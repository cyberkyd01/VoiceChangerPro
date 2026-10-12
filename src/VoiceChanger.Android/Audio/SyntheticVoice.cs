namespace VoiceChangerPro.AndroidApp.Audio;

/// <summary>
/// A voice-like test signal (band-limited glottal buzz with optional vibrato and syllable envelope).
/// Used by the DSP self-test and, when the app is launched with the "synthetic" test extra, in place of
/// the microphone so the whole live and record path can be verified on an emulator with no real voice.
/// </summary>
public sealed class SyntheticVoice
{
    private readonly float _sampleRate;
    private double _phase;
    private long _n;

    public SyntheticVoice(float sampleRate, float baseHz, bool modulate)
    {
        _sampleRate = sampleRate;
        BaseHz = baseHz;
        Modulate = modulate;
    }

    public float BaseHz { get; set; }
    public bool Modulate { get; set; }
    public float Amplitude { get; set; } = 0.25f;

    public void Fill(Span<float> buffer)
    {
        for (int i = 0; i < buffer.Length; i++, _n++)
        {
            double t = _n / (double)_sampleRate;
            double f0 = BaseHz;
            double env = 1.0;
            if (Modulate)
            {
                f0 *= 1.0 + 0.04 * Math.Sin(2 * Math.PI * 0.7 * t);           // slow intonation
                double syl = Math.Sin(2 * Math.PI * 2.2 * t);                    // ~4.4 syllables per second
                env = syl > 0 ? 0.25 + 0.75 * Math.Sqrt(syl) : 0.25;
            }
            _phase += 2 * Math.PI * f0 / _sampleRate;
            if (_phase > 2 * Math.PI) _phase -= 2 * Math.PI;

            // First 12 harmonics with a 1/k roll-off: a buzzy, vowel-like source.
            double s = 0;
            for (int k = 1; k <= 12; k++)
            {
                if (k * f0 > _sampleRate * 0.45) break;
                s += Math.Sin(k * _phase) / k;
            }
            buffer[i] = (float)(Amplitude * env * s * 0.55);
        }
    }

    /// <summary>Renders a block of a steady tone (no modulation).</summary>
    public static float[] Tone(float sampleRate, float hz, double seconds, float amplitude = 0.3f)
    {
        var gen = new SyntheticVoice(sampleRate, hz, modulate: false) { Amplitude = amplitude };
        var data = new float[(int)(sampleRate * seconds)];
        gen.Fill(data);
        return data;
    }
}
