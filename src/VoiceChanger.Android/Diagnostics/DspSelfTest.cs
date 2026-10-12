using System.Diagnostics;
using System.Text;
using VoiceChanger.Core.Dsp;
using VoiceChanger.Core.Presets;
using VoiceChangerPro.AndroidApp.Audio;

namespace VoiceChangerPro.AndroidApp.Diagnostics;

/// <summary>
/// On-device check that the voice engine really changes pitch. A synthetic 200 Hz voice-like tone is rendered
/// offline (same block-based path as clips) through pitch-shift profiles, and the pitch of the result is measured
/// with an independent autocorrelation estimator. Results go to logcat with the tag "VoiceChangerPro".
///
/// Run it with:  adb shell am start -n com.cyberkyd.voicechangerpro/.MainActivity --ez selftest true
/// or long-press the app title.
/// </summary>
public static class DspSelfTest
{
    public sealed record Case(string Name, float InputHz, float ExpectedHz, float MeasuredHz, double CentsError, double RealtimeFactor)
    {
        public bool Passed => MeasuredHz > 0 && Math.Abs(CentsError) <= 35;
    }

    public static IReadOnlyList<Case> Run()
    {
        const float inputHz = 200f;
        const double seconds = 3.0;
        float sr = AudioIo.SampleRate;
        var tone = SyntheticVoice.Tone(sr, inputHz, seconds);

        var cases = new List<(string name, VoiceProfile profile)>
        {
            ("Pitch +5 semitones", new VoiceProfile { PitchSemitones = 5 }),
            ("Pitch -5 semitones", new VoiceProfile { PitchSemitones = -5 }),
        };
        var preset = BuiltInPresets.All.FirstOrDefault(p => p.Id == "builtin-corporate-professional-woman")
                     ?? BuiltInPresets.All.FirstOrDefault(p => Math.Abs(p.Profile.PitchSemitones - 5f) < 0.01f);
        if (preset != null) cases.Add(($"{preset.Name} (+5)", preset.Profile.Clone()));

        var results = new List<Case>();
        float measuredInput = PitchEstimator.Median(tone, sr, 0.5, 2.5);
        AppLog.Info("SelfTest", $"Input tone: generated {inputHz} Hz, measured {measuredInput:F1} Hz");

        foreach (var (name, profile) in cases)
        {
            var sw = Stopwatch.StartNew();
            // Background noise processing off: a perfectly steady tone would otherwise be learned as "noise".
            var output = OfflineRenderer.Render(tone, profile, GlobalProcessing.Off);
            sw.Stop();
            float expected = inputHz * MathF.Pow(2f, profile.PitchSemitones / 12f);
            float measured = PitchEstimator.Median(output, sr, 1.0, 2.6);
            double cents = measured > 0 ? 1200 * Math.Log2(measured / expected) : double.NaN;
            double rtf = seconds / sw.Elapsed.TotalSeconds;
            var c = new Case(name, inputHz, expected, measured, cents, rtf);
            results.Add(c);
            AppLog.Info("SelfTest", $"{(c.Passed ? "PASS" : "FAIL")} {name}: expected {expected:F1} Hz, measured {measured:F1} Hz " +
                                    $"({cents:+0.0;-0.0} cents), rendered {rtf:F1}x faster than real time");
        }
        AppLog.Info("SelfTest", results.All(r => r.Passed) ? "SELFTEST PASSED" : "SELFTEST FAILED");
        LogLevels(sr);
        return results;
    }

    /// <summary>
    /// Informational: how loud a speech-like signal comes out of the default voice with and without
    /// background noise reduction (logged only).
    /// </summary>
    private static void LogLevels(float sr)
    {
        var speech = new float[(int)(sr * 4)];
        new SyntheticVoice(sr, 140, modulate: true).Fill(speech);
        var profile = (BuiltInPresets.All.FirstOrDefault(p => p.Id == "builtin-soft-warm-woman") ?? BuiltInPresets.All[0]).Profile;
        foreach (float nr in new[] { 0f, 0.5f, 1f })
        {
            var output = OfflineRenderer.Render(speech, profile.Clone(), new GlobalProcessing(nr, 0));
            double gain = 20 * Math.Log10(Rms(output, (int)sr, speech.Length) / Math.Max(1e-9, Rms(speech, (int)sr, speech.Length)));
            AppLog.Info("SelfTest", $"Level: speech-like input through 'Soft & Warm Woman' with noise reduction {nr * 100:F0}% changes level by {gain:+0.0;-0.0} dB");
        }
    }

    private static double Rms(float[] x, int from, int to)
    {
        double sum = 0;
        to = Math.Min(to, x.Length);
        for (int i = from; i < to; i++) sum += x[i] * x[i];
        return Math.Sqrt(sum / Math.Max(1, to - from));
    }

    public static string Format(IReadOnlyList<Case> results)
    {
        var sb = new StringBuilder();
        foreach (var r in results)
        {
            sb.Append(r.Passed ? "✓ " : "✗ ").Append(r.Name).Append('\n')
              .Append($"    {r.MeasuredHz:F1} Hz (expected {r.ExpectedHz:F1}, {r.CentsError:+0;-0} cents)\n")
              .Append($"    {r.RealtimeFactor:F1}× faster than real time\n");
        }
        sb.Append(results.All(r => r.Passed) ? "\nAll checks passed." : "\nSome checks failed.");
        return sb.ToString();
    }
}

/// <summary>Normalised square-difference (McLeod) pitch estimator, independent of the engine's own tracker.</summary>
public static class PitchEstimator
{
    /// <summary>Median pitch of 2048-sample windows between two times (seconds). 0 when unvoiced.</summary>
    public static float Median(float[] x, float sampleRate, double fromSec, double toSec)
    {
        const int window = 2048;
        int start = (int)(fromSec * sampleRate);
        int end = Math.Min(x.Length - window, (int)(toSec * sampleRate));
        var estimates = new List<float>();
        for (int pos = start; pos < end; pos += window)
        {
            float f = Estimate(x.AsSpan(pos, window), sampleRate);
            if (f > 0) estimates.Add(f);
        }
        if (estimates.Count == 0) return 0;
        estimates.Sort();
        return estimates[estimates.Count / 2];
    }

    public static float Estimate(ReadOnlySpan<float> x, float sampleRate, float minHz = 60, float maxHz = 800)
    {
        int minLag = (int)(sampleRate / maxHz);
        int maxLag = Math.Min(x.Length / 2, (int)(sampleRate / minHz));
        var nsdf = new double[maxLag + 2];
        double best = 0;
        for (int lag = minLag; lag <= maxLag + 1; lag++)
        {
            double acf = 0, energy = 0;
            int n = x.Length - lag;
            for (int i = 0; i < n; i++)
            {
                acf += x[i] * x[i + lag];
                energy += x[i] * x[i] + x[i + lag] * x[i + lag];
            }
            nsdf[lag] = energy > 1e-9 ? 2 * acf / energy : 0;
            if (nsdf[lag] > best) best = nsdf[lag];
        }
        if (best < 0.5) return 0; // not periodic enough

        // First local maximum that reaches 90 % of the best one (avoids picking a sub-octave).
        for (int lag = minLag + 1; lag <= maxLag; lag++)
        {
            if (nsdf[lag] >= 0.9 * best && nsdf[lag] >= nsdf[lag - 1] && nsdf[lag] >= nsdf[lag + 1])
            {
                double a = nsdf[lag - 1], b = nsdf[lag], c = nsdf[lag + 1];
                double denom = a - 2 * b + c;
                double shift = Math.Abs(denom) > 1e-12 ? 0.5 * (a - c) / denom : 0;
                return (float)(sampleRate / (lag + shift));
            }
        }
        return 0;
    }
}
