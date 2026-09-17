using VoiceChanger.Core.Dsp;
using Xunit;
using Xunit.Abstractions;

namespace VoiceChanger.Core.Tests;

public class PsolaAndNoiseTests
{
    private const float Fs = 48000f;
    private readonly ITestOutputHelper _out;
    public PsolaAndNoiseTests(ITestOutputHelper output) => _out = output;

    /// <summary>Glottal-pulse-like vowel: impulse train at f0 through resonances at 500 / 1500 / 2500 Hz.</summary>
    private static float[] Vowel(float seconds, float f0, float amp = 0.4f)
    {
        int n = (int)(seconds * Fs);
        var x = new float[n];
        double period = Fs / f0;
        double next = 0;
        for (int i = 0; i < n; i++)
        {
            if (i >= next) { x[i] = 1f; next += period; }
        }
        // vocal-tract resonances (three 2nd-order sections) shape the pulses
        var f1 = new Biquad(Fs, BiquadType.BandPass, 500, 6);
        var f2 = new Biquad(Fs, BiquadType.BandPass, 1500, 8);
        var f3 = new Biquad(Fs, BiquadType.BandPass, 2500, 10);
        var y = new float[n];
        for (int i = 0; i < n; i++) y[i] = f1.Process(x[i]) * 1.0f + f2.Process(x[i]) * 0.5f + f3.Process(x[i]) * 0.3f;
        float peak = y.Max(v => MathF.Abs(v));
        for (int i = 0; i < n; i++) y[i] = y[i] / peak * amp;
        return y;
    }

    private static void ProcessInBlocks(Action<float[]> process, float[] signal, int block = 480)
    {
        var tmp = new float[block];
        for (int i = 0; i + block <= signal.Length; i += block)
        {
            Array.Copy(signal, i, tmp, 0, block);
            process(tmp);
            Array.Copy(tmp, 0, signal, i, block);
        }
    }

    private static double[] Spectrum(ReadOnlySpan<float> x, int n)
    {
        var re = new double[n]; var im = new double[n];
        for (int i = 0; i < n && i < x.Length; i++) re[i] = x[i] * (0.5 - 0.5 * Math.Cos(2 * Math.PI * i / n));
        new Fft(n).Forward(re, im);
        var mag = new double[n / 2];
        for (int k = 0; k < n / 2; k++) mag[k] = Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
        return mag;
    }

    private static double BandEnergy(double[] mag, double lo, double hi, int n)
    {
        double e = 0;
        for (int k = 0; k < mag.Length; k++) { double f = k * Fs / n; if (f >= lo && f < hi) e += mag[k] * mag[k]; }
        return e;
    }

    /// <summary>Fundamental estimate from the harmonic comb: the spacing that maximises summed harmonic energy.</summary>
    private static double EstimateF0(double[] mag, int n, double lo, double hi)
    {
        var energies = new List<(double f, double e)>();
        double bestE = -1;
        for (double f = lo; f <= hi; f += 0.5)
        {
            double e = 0;
            for (int h = 1; h <= 24; h++)
            {
                double k = h * f * n / Fs;
                int ki = (int)Math.Round(k);
                if (ki + 1 < mag.Length) e += Math.Max(mag[ki - 1], Math.Max(mag[ki], mag[ki + 1]));
            }
            energies.Add((f, e));
            if (e > bestE) bestE = e;
        }
        // the lowest candidate whose comb captures nearly all the energy is the fundamental (sub-multiples share harmonics)
        return energies.First(x => x.e >= bestE * 0.92).f;
    }

    [Theory]
    [InlineData(120f, 1.5)]
    [InlineData(120f, 0.7)]
    [InlineData(200f, 1.8)]
    [InlineData(110f, 0.6)]
    public void Psola_ShiftsPitch_PreservesFormants(float f0, double ratio)
    {
        var ps = new PsolaPitchShifter(Fs);
        ps.SetRatio(ratio);
        var sig = Vowel(1.4f, f0);
        var orig = (float[])sig.Clone();
        ProcessInBlocks(b => ps.Process(b), sig);
        Assert.True(sig.All(v => !float.IsNaN(v)));

        const int N = 32768;
        var seg = sig.AsSpan(sig.Length - N - 2400, N);
        var mag = Spectrum(seg, N);
        var magOrig = Spectrum(orig.AsSpan(orig.Length - N - 2400, N), N);

        double est = EstimateF0(mag, N, 60, 450);
        double expected = f0 * ratio;
        _out.WriteLine($"f0={f0} ratio={ratio} → measured {est:F1} Hz (expected {expected:F1}), tracker {ps.DetectedPitchHz:F1} Hz");
        Assert.InRange(est, expected * 0.96, expected * 1.04);
        Assert.InRange(ps.DetectedPitchHz, f0 * 0.97, f0 * 1.03);

        // formant preservation: the 500 Hz resonance must still dominate over 1500 Hz by a similar margin
        // wide bands so that every harmonic grid samples each resonance; a WSOLA-style shifter would move
        // the resonances by the pitch ratio and change this ratio by a factor of 3 or more.
        double r1o = BandEnergy(magOrig, 250, 850, N) / BandEnergy(magOrig, 1150, 1950, N);
        double r1 = BandEnergy(mag, 250, 850, N) / BandEnergy(mag, 1150, 1950, N);
        _out.WriteLine($"F1/F2 energy ratio original {r1o:F2}, shifted {r1:F2}");
        Assert.True(r1 > r1o * 0.35 && r1 < r1o * 3.0, $"spectral envelope should be preserved by PSOLA (ratio {r1o:F2} → {r1:F2})");

        // level roughly preserved (within 4 dB)
        double rmsIn = Math.Sqrt(orig.Skip(orig.Length / 2).Select(v => (double)v * v).Average());
        double rmsOut = Math.Sqrt(sig.Skip(sig.Length / 2).Select(v => (double)v * v).Average());
        _out.WriteLine($"rms in {rmsIn:F4} out {rmsOut:F4}");
        Assert.InRange(rmsOut / rmsIn, 0.63, 1.6);
    }

    [Theory]
    [InlineData(1.5)]
    [InlineData(0.75)]
    [InlineData(1.26)]
    public void Psola_SteadyVowel_HasNoLevelDropsOrGaps(double ratio)
    {
        // A steady vowel through the shifter must come out steady: short-term level must not fluctuate
        // (this is what "frame drops" / rough "throaty" texture look like numerically).
        var ps = new PsolaPitchShifter(Fs);
        ps.SetRatio(ratio);
        var sig = Vowel(1.5f, 125);
        ProcessInBlocks(b => ps.Process(b), sig);
        int win = 960; // 20 ms
        var levels = new List<double>();
        for (int s = sig.Length / 2; s + win < sig.Length - 2400; s += win)
        {
            double e = 0;
            for (int i = 0; i < win; i++) e += sig[s + i] * sig[s + i];
            levels.Add(Math.Sqrt(e / win));
        }
        double mean = levels.Average();
        double sd = Math.Sqrt(levels.Select(l => (l - mean) * (l - mean)).Average());
        double cv = sd / mean;
        _out.WriteLine($"ratio {ratio}: mean level {mean:F4}, coefficient of variation {cv:P1}, min/mean {levels.Min() / mean:P0}");
        Assert.True(cv < 0.12, $"short-term level varies too much ({cv:P1})");
        Assert.True(levels.Min() / mean > 0.7, "level dips (dropouts) detected");
    }

    [Fact]
    public void Psola_Unvoiced_PassesNoiseUnchangedInLevel()
    {
        var ps = new PsolaPitchShifter(Fs);
        ps.SetRatio(1.6);
        var rnd = new Random(3);
        var sig = new float[48000];
        for (int i = 0; i < sig.Length; i++) sig[i] = (float)(rnd.NextDouble() - 0.5) * 0.4f;
        var orig = (float[])sig.Clone();
        ProcessInBlocks(b => ps.Process(b), sig);
        double rmsIn = Math.Sqrt(orig.Skip(24000).Select(v => (double)v * v).Average());
        double rmsOut = Math.Sqrt(sig.Skip(24000).Select(v => (double)v * v).Average());
        Assert.InRange(rmsOut / rmsIn, 0.7, 1.4);
        Assert.Equal(0, ps.DetectedPitchHz);
    }

    [Fact]
    public void Psola_Bypass_IsDelayedIdentity()
    {
        var ps = new PsolaPitchShifter(Fs);
        ps.SetRatio(1.0);
        var sig = Vowel(0.5f, 150);
        var copy = (float[])sig.Clone();
        ProcessInBlocks(b => ps.Process(b), sig);
        int d = ps.LatencySamples;
        for (int i = d + 10; i < sig.Length - 480; i++)
            Assert.InRange(sig[i], copy[i - d] - 1e-4, copy[i - d] + 1e-4);
    }

    [Fact]
    public void NoiseSuppressor_ImprovesSnr_KeepsSpeechLevel()
    {
        var ns = new NoiseSuppressor(Fs);
        ns.Configure(0.8f);
        var rnd = new Random(11);
        int n = (int)(Fs * 4);
        var speech = Vowel(4f, 130, 0.3f);
        // first half noise-only so the estimator settles; second half syllable-like bursts (300 ms on / 250 ms off)
        int burstOn = 14400, burstOff = 12000;
        for (int i = 0; i < n; i++)
        {
            if (i < n / 2) { speech[i] = 0; continue; }
            int t = (i - n / 2) % (burstOn + burstOff);
            if (t >= burstOn) speech[i] = 0;
        }
        var noise = new float[n];
        for (int i = 0; i < n; i++) noise[i] = (float)(rnd.NextDouble() - 0.5) * 0.06f; // ~ -30 dBFS white noise
        var mix = new float[n];
        for (int i = 0; i < n; i++) mix[i] = speech[i] + noise[i];
        ProcessInBlocks(b => ns.Process(b), mix);
        Assert.True(mix.All(v => !float.IsNaN(v)));

        // noise-only region (last 0.5 s of the first half) should be attenuated strongly
        double noiseIn = Math.Sqrt(noise.Skip(n / 2 - 24000).Take(24000).Select(v => (double)v * v).Average());
        double noiseOut = Math.Sqrt(mix.Skip(n / 2 - 24000).Take(24000).Select(v => (double)v * v).Average());
        double reductionDb = 20 * Math.Log10(noiseOut / noiseIn);
        _out.WriteLine($"noise-only reduction {reductionDb:F1} dB, floor readout {ns.NoiseFloorDb:F1} dBFS");
        Assert.True(reductionDb < -12, $"expected >12 dB noise reduction, got {-reductionDb:F1}");

        // speech region: level of the last few bursts (skipping each burst's first 60 ms) within 3 dB of the clean level
        var burstIdx = Enumerable.Range(n / 2, n / 2)
            .Where(i => { int t = (i - n / 2) % (burstOn + burstOff); return t >= 2880 && t < burstOn; })
            .Where(i => i >= n - 3 * (burstOn + burstOff)).ToList();
        double speechIn = Math.Sqrt(burstIdx.Select(i => (double)speech[i] * speech[i]).Average());
        double speechOut = Math.Sqrt(burstIdx.Select(i => (double)mix[i] * mix[i]).Average());
        double speechDb = 20 * Math.Log10(speechOut / speechIn);
        _out.WriteLine($"speech level change {speechDb:F1} dB");
        Assert.InRange(speechDb, -3, 2);
    }

    [Fact]
    public void NoiseSuppressor_Off_IsDelayedIdentity()
    {
        var ns = new NoiseSuppressor(Fs);
        ns.Configure(0);
        var sig = Vowel(0.4f, 150);
        var copy = (float[])sig.Clone();
        ProcessInBlocks(b => ns.Process(b), sig);
        int d = ns.LatencySamples;
        for (int i = d + 10; i < sig.Length - 480; i++)
            Assert.InRange(sig[i], copy[i - d] - 1e-5, copy[i - d] + 1e-5);
    }

    [Fact]
    public void VoiceIsolator_AttenuatesQuietSounds_KeepsLoudOnes()
    {
        var iso = new VoiceIsolator(Fs);
        iso.Configure(0.8f); // threshold -30 dBFS
        var loud = Vowel(0.5f, 140, 0.5f);   // ~ -9 dBFS peak
        var quiet = Vowel(0.5f, 140, 0.01f); // -40 dBFS peak
        iso.Process(loud);
        double loudOut = loud.Skip(12000).Max(v => MathF.Abs(v));
        Assert.InRange(loudOut, 0.4, 0.55);
        iso.Process(quiet);
        double quietOut = quiet.Skip(12000).Max(v => MathF.Abs(v));
        Assert.True(quietOut < 0.004, $"quiet background should be pushed down, got {quietOut}");
    }

    [Fact]
    public void Pipeline_GlobalSettings_ApplyLive_AndLatencyIsReported()
    {
        var pipe = new VoicePipeline(Fs);
        pipe.ApplyGlobal(new GlobalProcessing(0.6f, 0.3f));
        var sig = Vowel(0.5f, 130);
        ProcessInBlocks(b => pipe.Process(b), sig);
        Assert.Equal(0.6f, pipe.CurrentGlobal.NoiseReduction);
        Assert.True(pipe.LatencyMs > 40 && pipe.LatencyMs < 120, $"latency {pipe.LatencyMs:F1} ms");
        Assert.True(sig.All(v => !float.IsNaN(v)));
    }
}
