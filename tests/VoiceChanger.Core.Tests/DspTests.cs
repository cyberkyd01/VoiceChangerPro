using VoiceChanger.Core.Dsp;
using VoiceChanger.Core.Presets;
using Xunit;

namespace VoiceChanger.Core.Tests;

public class DspTests
{
    private const float Fs = 48000f;

    private static float[] Sine(float freq, float seconds, float amp = 0.5f)
    {
        int n = (int)(seconds * Fs);
        var b = new float[n];
        for (int i = 0; i < n; i++) b[i] = amp * MathF.Sin(2 * MathF.PI * freq * i / Fs);
        return b;
    }

    /// <summary>Dominant frequency of a signal segment via zero-padded FFT peak with parabolic interpolation.</summary>
    private static double DominantFrequency(ReadOnlySpan<float> x)
    {
        int n = 1; while (n < x.Length) n <<= 1;
        n <<= 1;
        var re = new double[n]; var im = new double[n];
        for (int i = 0; i < x.Length; i++) re[i] = x[i] * (0.5 - 0.5 * Math.Cos(2 * Math.PI * i / x.Length));
        new Fft(n).Forward(re, im);
        int best = 1; double bestMag = 0;
        for (int k = 1; k < n / 2; k++)
        {
            double m = re[k] * re[k] + im[k] * im[k];
            if (m > bestMag) { bestMag = m; best = k; }
        }
        double a = Math.Sqrt(re[best - 1] * re[best - 1] + im[best - 1] * im[best - 1]);
        double b = Math.Sqrt(bestMag);
        double c = Math.Sqrt(re[best + 1] * re[best + 1] + im[best + 1] * im[best + 1]);
        double p = 0.5 * (a - c) / (a - 2 * b + c);
        return (best + p) * Fs / n;
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

    [Fact]
    public void Fft_RoundTrip_IsIdentity()
    {
        var fft = new Fft(1024);
        var rnd = new Random(1);
        var re = new double[1024]; var im = new double[1024];
        var orig = new double[1024];
        for (int i = 0; i < 1024; i++) { orig[i] = rnd.NextDouble() * 2 - 1; re[i] = orig[i]; }
        fft.Forward(re, im);
        fft.Inverse(re, im);
        for (int i = 0; i < 1024; i++) Assert.InRange(re[i], orig[i] - 1e-9, orig[i] + 1e-9);
    }

    [Fact]
    public void Biquad_PeakingFilter_HasRequestedGainAtCenter()
    {
        var bq = new Biquad(Fs, BiquadType.Peaking, 1000, 1.0, 6);
        Assert.InRange(bq.MagnitudeDb(1000), 5.9, 6.1);
        Assert.InRange(bq.MagnitudeDb(50), -0.2, 0.2);
        var hp = new Biquad(Fs, BiquadType.HighPass, 100, 0.7071);
        Assert.True(hp.MagnitudeDb(20) < -20);
        Assert.InRange(hp.MagnitudeDb(2000), -0.1, 0.1);
    }

    [Theory]
    [InlineData(1.5)]
    [InlineData(0.66)]
    [InlineData(2.0)]
    public void PitchShifter_ShiftsFrequencyByRatio(double ratio)
    {
        var ps = new PitchShifter(Fs);
        ps.SetRatio(ratio);
        var sig = Sine(220, 1.0f);
        ProcessInBlocks(b => ps.Process(b), sig);
        // analyse the steady-state second half
        double f = DominantFrequency(sig.AsSpan(sig.Length / 2, 16384));
        double expected = 220 * ratio;
        Assert.InRange(f, expected * 0.97, expected * 1.03);
        Assert.True(sig.All(v => !float.IsNaN(v)));
        // level preserved within 3 dB
        double rms = Math.Sqrt(sig.Skip(sig.Length / 2).Select(v => (double)v * v).Average());
        Assert.InRange(rms, 0.5 / Math.Sqrt(2) * 0.7, 0.5 / Math.Sqrt(2) * 1.4);
    }

    [Fact]
    public void PitchShifter_Bypass_IsDelayedIdentity()
    {
        var ps = new PitchShifter(Fs);
        ps.SetRatio(1.0);
        var sig = Sine(300, 0.5f);
        var copy = (float[])sig.Clone();
        ProcessInBlocks(b => ps.Process(b), sig);
        int d = ps.LatencySamples;
        for (int i = d + 10; i < sig.Length - 480; i++)
            Assert.InRange(sig[i], copy[i - d] - 1e-4, copy[i - d] + 1e-4);
    }

    /// <summary>Synthetic vowel: 130 Hz harmonics shaped by resonances at 500 / 1500 / 2500 Hz.</summary>
    private static float[] Vowel(float seconds, float f0 = 130f)
    {
        int n = (int)(seconds * Fs);
        var b = new float[n];
        var formants = new[] { 500.0, 1500.0, 2500.0 };
        for (int h = 1; h * f0 < 6000; h++)
        {
            double f = h * f0;
            double env = 0;
            foreach (var fc in formants) env += Math.Exp(-Math.Pow((f - fc) / 120.0, 2));
            double amp = 0.15 * Math.Max(env, 0.003);
            for (int i = 0; i < n; i++) b[i] += (float)(amp * Math.Sin(2 * Math.PI * f * i / Fs + h));
        }
        return b;
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
        for (int k = 0; k < mag.Length; k++)
        {
            double f = k * Fs / n;
            if (f >= lo && f < hi) e += mag[k] * mag[k];
        }
        return e;
    }

    [Fact]
    public void FormantShifter_MovesEnvelope_KeepsPitch()
    {
        var fs = new FormantShifter(Fs);
        fs.Configure(1.4, 0);
        var sig = Vowel(1.2f);
        ProcessInBlocks(b => fs.Process(b), sig);
        var seg = sig.AsSpan(sig.Length / 2, 16384);
        Assert.True(sig.All(v => !float.IsNaN(v)));

        // pitch preserved: strongest low harmonic still at 130 Hz multiples
        var mag = Spectrum(seg, 32768);
        // first formant moved from ~500 Hz to ~700 Hz: energy around 700 should now exceed energy around 500
        double e500 = BandEnergy(mag, 420, 580, 32768);
        double e700 = BandEnergy(mag, 620, 800, 32768);
        Assert.True(e700 > e500, $"expected energy at 700 Hz ({e700:E2}) to exceed 500 Hz ({e500:E2}) after upward formant shift");

        // harmonic structure intact: peaks at multiples of 130 Hz
        int k130 = (int)Math.Round(130 * 32768 / Fs);
        int k195 = (int)Math.Round(195 * 32768 / Fs); // between harmonics
        Assert.True(mag[k130 * 5] > mag[k195 * 3] * 3, "harmonic peaks should dominate inter-harmonic bins");
        Assert.InRange(fs.DetectedPitchHz, 120, 140);
    }

    [Fact]
    public void FormantShifter_Bypass_IsDelayedIdentity()
    {
        var fs = new FormantShifter(Fs);
        fs.Configure(1.0, 0);
        var sig = Sine(300, 0.5f);
        var copy = (float[])sig.Clone();
        ProcessInBlocks(b => fs.Process(b), sig);
        int d = fs.LatencySamples;
        for (int i = d + 10; i < sig.Length - 480; i++)
            Assert.InRange(sig[i], copy[i - d] - 1e-4, copy[i - d] + 1e-4);
    }

    [Fact]
    public void FormantShifter_UnityRatioProcessing_IsNearTransparent()
    {
        // ratio very slightly off 1 forces the STFT path; output should closely match input level and pitch
        var fs = new FormantShifter(Fs);
        fs.Configure(1.01, 0);
        var sig = Vowel(1.0f);
        var copy = (float[])sig.Clone();
        ProcessInBlocks(b => fs.Process(b), sig);
        double rmsIn = Math.Sqrt(copy.Skip(copy.Length / 2).Select(v => (double)v * v).Average());
        double rmsOut = Math.Sqrt(sig.Skip(sig.Length / 2).Select(v => (double)v * v).Average());
        Assert.InRange(rmsOut / rmsIn, 0.7, 1.4);
    }

    [Fact]
    public void Pipeline_AllBuiltInPresets_AreStableOnSpeechLikeSignal()
    {
        var sig = Vowel(0.6f);
        var rnd = new Random(7);
        for (int i = 0; i < sig.Length; i++) sig[i] += (float)(rnd.NextDouble() - 0.5) * 0.01f;
        foreach (var preset in BuiltInPresets.All)
        {
            var pipe = new VoicePipeline(Fs);
            pipe.Apply(preset.Profile);
            var work = (float[])sig.Clone();
            ProcessInBlocks(b => pipe.Process(b), work);
            Assert.True(work.All(v => !float.IsNaN(v) && !float.IsInfinity(v)), $"{preset.Name}: NaN/Inf in output");
            float peak = work.Max(v => MathF.Abs(v));
            Assert.True(peak <= 1.0001f, $"{preset.Name}: peak {peak} exceeds full scale");
            double rms = Math.Sqrt(work.Skip(work.Length / 2).Select(v => (double)v * v).Average());
            Assert.True(rms > 1e-4, $"{preset.Name}: output is silent (rms {rms:E2})");
        }
    }

    [Fact]
    public void Pipeline_Bypass_PassesAudioUntouched()
    {
        var pipe = new VoicePipeline(Fs) { Bypass = true };
        var p = new VoiceProfile { PitchSemitones = 12 };
        pipe.Apply(p);
        var sig = Sine(440, 0.4f);
        var copy = (float[])sig.Clone();
        ProcessInBlocks(b => pipe.Process(b), sig);
        // bypass still passes through the (inactive) global stage, which is a pure delay
        int d = new NoiseSuppressor(Fs).LatencySamples;
        for (int i = d + 10; i < sig.Length - 480; i++)
            Assert.InRange(sig[i], copy[i - d] - 1e-5, copy[i - d] + 1e-5);
    }

    [Fact]
    public void NoiseGate_ClosesOnSilence_OpensOnSignal()
    {
        var gate = new NoiseGate(Fs);
        gate.Configure(-40, 1, 20, 0);
        var loud = Sine(440, 0.2f, 0.5f);
        gate.Process(loud);
        Assert.True(gate.IsOpen);
        Assert.True(loud.Skip(4000).Max(v => MathF.Abs(v)) > 0.4f);
        var quiet = Sine(440, 0.3f, 0.001f);
        gate.Process(quiet);
        Assert.False(gate.IsOpen);
        Assert.True(quiet.Skip(quiet.Length - 1000).Max(v => MathF.Abs(v)) < 1e-4f);
    }

    [Fact]
    public void Compressor_ReducesLoudSignals()
    {
        var comp = new Compressor(Fs);
        comp.Configure(-20, 4, 1, 50, 0, 0);
        var sig = Sine(440, 0.5f, 0.9f); // ~ -0.9 dBFS peak
        comp.Process(sig);
        double peak = sig.Skip(sig.Length / 2).Max(v => MathF.Abs(v));
        // 19 dB over threshold at 4:1 → ~14 dB reduction → peak ≈ 0.18
        Assert.InRange(peak, 0.12, 0.3);
        Assert.True(comp.GainReductionDb < -8);
    }

    [Fact]
    public void Limiter_NeverExceedsCeiling()
    {
        var lim = new Limiter(Fs);
        lim.Configure(-1);
        var sig = Sine(440, 0.2f, 2.0f);
        lim.Process(sig);
        float ceiling = DspUtil.DbToLinear(-1);
        Assert.True(sig.All(v => MathF.Abs(v) <= ceiling + 1e-5f));
    }
}
