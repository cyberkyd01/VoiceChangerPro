namespace VoiceChanger.Core.Dsp;

/// <summary>
/// In-place iterative radix-2 complex FFT with cached twiddle factors and bit-reversal table.
/// Sized once for a fixed power-of-two length.
/// </summary>
public sealed class Fft
{
    private readonly int _n;
    private readonly int _log2n;
    private readonly int[] _rev;
    private readonly double[] _cos;
    private readonly double[] _sin;

    public int Length => _n;

    public Fft(int n)
    {
        if (n < 2 || (n & (n - 1)) != 0) throw new ArgumentException("FFT size must be a power of two", nameof(n));
        _n = n;
        _log2n = (int)Math.Log2(n);
        _rev = new int[n];
        for (int i = 0; i < n; i++)
        {
            int r = 0, x = i;
            for (int b = 0; b < _log2n; b++) { r = (r << 1) | (x & 1); x >>= 1; }
            _rev[i] = r;
        }
        _cos = new double[n / 2];
        _sin = new double[n / 2];
        for (int i = 0; i < n / 2; i++)
        {
            double a = -2 * Math.PI * i / n;
            _cos[i] = Math.Cos(a);
            _sin[i] = Math.Sin(a);
        }
    }

    /// <summary>Forward transform (no scaling).</summary>
    public void Forward(double[] re, double[] im) => Transform(re, im, false);

    /// <summary>Inverse transform (scaled by 1/N).</summary>
    public void Inverse(double[] re, double[] im)
    {
        Transform(re, im, true);
        double s = 1.0 / _n;
        for (int i = 0; i < _n; i++) { re[i] *= s; im[i] *= s; }
    }

    private void Transform(double[] re, double[] im, bool inverse)
    {
        int n = _n;
        for (int i = 0; i < n; i++)
        {
            int j = _rev[i];
            if (j > i)
            {
                (re[i], re[j]) = (re[j], re[i]);
                (im[i], im[j]) = (im[j], im[i]);
            }
        }

        for (int size = 2; size <= n; size <<= 1)
        {
            int half = size >> 1;
            int step = n / size;
            for (int start = 0; start < n; start += size)
            {
                int k = 0;
                for (int j = start; j < start + half; j++, k += step)
                {
                    double wr = _cos[k];
                    double wi = inverse ? -_sin[k] : _sin[k];
                    int l = j + half;
                    double tr = re[l] * wr - im[l] * wi;
                    double ti = re[l] * wi + im[l] * wr;
                    re[l] = re[j] - tr;
                    im[l] = im[j] - ti;
                    re[j] += tr;
                    im[j] += ti;
                }
            }
        }
    }
}
