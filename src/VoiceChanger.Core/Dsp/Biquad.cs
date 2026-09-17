namespace VoiceChanger.Core.Dsp;

public enum BiquadType
{
    LowPass,
    HighPass,
    BandPass,
    Notch,
    Peaking,
    LowShelf,
    HighShelf,
    AllPass
}

/// <summary>
/// Second-order IIR filter (RBJ Audio EQ Cookbook), transposed direct form II.
/// Coefficients are recomputed only when parameters change.
/// </summary>
public sealed class Biquad
{
    private double _b0 = 1, _b1, _b2, _a1, _a2;
    private double _z1, _z2;

    private BiquadType _type;
    private double _fs, _freq, _q, _gainDb;
    private bool _configured;

    public Biquad(double sampleRate, BiquadType type, double freq, double q = 0.7071, double gainDb = 0)
    {
        _fs = sampleRate;
        Configure(type, freq, q, gainDb);
    }

    public double SampleRate => _fs;

    /// <summary>Sets the filter parameters. No-op when nothing changed.</summary>
    public void Configure(BiquadType type, double freq, double q = 0.7071, double gainDb = 0)
    {
        if (_configured && type == _type && Math.Abs(freq - _freq) < 1e-6 && Math.Abs(q - _q) < 1e-6 && Math.Abs(gainDb - _gainDb) < 1e-6)
            return;

        _configured = true;
        _type = type;
        _freq = Math.Clamp(freq, 1.0, _fs * 0.499);
        _q = Math.Max(q, 0.05);
        _gainDb = gainDb;

        double A = Math.Pow(10, gainDb / 40);
        double w0 = 2 * Math.PI * _freq / _fs;
        double cos = Math.Cos(w0), sin = Math.Sin(w0);
        double alpha = sin / (2 * _q);
        double b0, b1, b2, a0, a1, a2;

        switch (type)
        {
            case BiquadType.LowPass:
                b0 = (1 - cos) / 2; b1 = 1 - cos; b2 = (1 - cos) / 2;
                a0 = 1 + alpha; a1 = -2 * cos; a2 = 1 - alpha;
                break;
            case BiquadType.HighPass:
                b0 = (1 + cos) / 2; b1 = -(1 + cos); b2 = (1 + cos) / 2;
                a0 = 1 + alpha; a1 = -2 * cos; a2 = 1 - alpha;
                break;
            case BiquadType.BandPass: // constant 0 dB peak gain
                b0 = alpha; b1 = 0; b2 = -alpha;
                a0 = 1 + alpha; a1 = -2 * cos; a2 = 1 - alpha;
                break;
            case BiquadType.Notch:
                b0 = 1; b1 = -2 * cos; b2 = 1;
                a0 = 1 + alpha; a1 = -2 * cos; a2 = 1 - alpha;
                break;
            case BiquadType.AllPass:
                b0 = 1 - alpha; b1 = -2 * cos; b2 = 1 + alpha;
                a0 = 1 + alpha; a1 = -2 * cos; a2 = 1 - alpha;
                break;
            case BiquadType.Peaking:
                b0 = 1 + alpha * A; b1 = -2 * cos; b2 = 1 - alpha * A;
                a0 = 1 + alpha / A; a1 = -2 * cos; a2 = 1 - alpha / A;
                break;
            case BiquadType.LowShelf:
            {
                double sqA = Math.Sqrt(A);
                double beta = 2 * sqA * alpha;
                b0 = A * ((A + 1) - (A - 1) * cos + beta);
                b1 = 2 * A * ((A - 1) - (A + 1) * cos);
                b2 = A * ((A + 1) - (A - 1) * cos - beta);
                a0 = (A + 1) + (A - 1) * cos + beta;
                a1 = -2 * ((A - 1) + (A + 1) * cos);
                a2 = (A + 1) + (A - 1) * cos - beta;
                break;
            }
            case BiquadType.HighShelf:
            {
                double sqA = Math.Sqrt(A);
                double beta = 2 * sqA * alpha;
                b0 = A * ((A + 1) + (A - 1) * cos + beta);
                b1 = -2 * A * ((A - 1) + (A + 1) * cos);
                b2 = A * ((A + 1) + (A - 1) * cos - beta);
                a0 = (A + 1) - (A - 1) * cos + beta;
                a1 = 2 * ((A - 1) - (A + 1) * cos);
                a2 = (A + 1) - (A - 1) * cos - beta;
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }

        _b0 = b0 / a0; _b1 = b1 / a0; _b2 = b2 / a0;
        _a1 = a1 / a0; _a2 = a2 / a0;
    }

    public void Reset() { _z1 = 0; _z2 = 0; }

    public float Process(float x)
    {
        double y = _b0 * x + _z1;
        _z1 = _b1 * x - _a1 * y + _z2;
        _z2 = _b2 * x - _a2 * y;
        return (float)y;
    }

    public void Process(Span<float> buffer)
    {
        double b0 = _b0, b1 = _b1, b2 = _b2, a1 = _a1, a2 = _a2, z1 = _z1, z2 = _z2;
        for (int i = 0; i < buffer.Length; i++)
        {
            double x = buffer[i];
            double y = b0 * x + z1;
            z1 = b1 * x - a1 * y + z2;
            z2 = b2 * x - a2 * y;
            buffer[i] = (float)y;
        }
        _z1 = z1; _z2 = z2;
        if (double.IsNaN(_z1) || double.IsInfinity(_z1) || double.IsNaN(_z2) || double.IsInfinity(_z2)) Reset();
    }

    /// <summary>Magnitude response in dB at the given frequency (for UI curves and tests).</summary>
    public double MagnitudeDb(double freq)
    {
        double w = 2 * Math.PI * freq / _fs;
        var z = new System.Numerics.Complex(Math.Cos(w), -Math.Sin(w)); // e^{-jw}
        var num = _b0 + _b1 * z + _b2 * z * z;
        var den = 1 + _a1 * z + _a2 * z * z;
        return 20 * Math.Log10((num / den).Magnitude + 1e-12);
    }
}
