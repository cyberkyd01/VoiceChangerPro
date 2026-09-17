namespace VoiceChanger.Core.Dsp;

public static class DspUtil
{
    public const float MinusInfinityDb = -120f;

    public static float DbToLinear(float db) => db <= MinusInfinityDb ? 0f : MathF.Pow(10f, db / 20f);

    public static float LinearToDb(float lin) => lin <= 1e-6f ? MinusInfinityDb : 20f * MathF.Log10(lin);

    public static float SemitonesToRatio(float semitones) => MathF.Pow(2f, semitones / 12f);

    /// <summary>One-pole coefficient for a time constant in milliseconds.</summary>
    public static float TimeConstant(float ms, float sampleRate)
    {
        if (ms <= 0) return 1f;
        return 1f - MathF.Exp(-1f / (0.001f * ms * sampleRate));
    }

    public static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>4-point Hermite interpolation.</summary>
    public static float Hermite(float xm1, float x0, float x1, float x2, float t)
    {
        float c = (x1 - xm1) * 0.5f;
        float v = x0 - x1;
        float w = c + v;
        float a = w + v + (x2 - x0) * 0.5f;
        float b = w + a;
        return ((a * t - b) * t + c) * t + x0;
    }

    public static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

    /// <summary>Replaces NaN/Inf with 0 and hard-clips to ±4 to stop runaway feedback.</summary>
    public static void Sanitize(Span<float> buf)
    {
        for (int i = 0; i < buf.Length; i++)
        {
            float v = buf[i];
            if (float.IsNaN(v) || float.IsInfinity(v)) v = 0;
            else if (v > 4f) v = 4f;
            else if (v < -4f) v = -4f;
            buf[i] = v;
        }
    }
}

/// <summary>Linear per-sample parameter smoother to avoid zipper noise.</summary>
public sealed class SmoothedValue
{
    private float _current, _target, _step;
    private int _remaining;
    private readonly int _rampSamples;

    public SmoothedValue(float initial, float sampleRate, float rampMs = 20f)
    {
        _current = _target = initial;
        _rampSamples = Math.Max(1, (int)(rampMs * 0.001f * sampleRate));
    }

    public float Target => _target;
    public float Current => _current;
    public bool IsSmoothing => _remaining > 0;

    public void SetTarget(float value)
    {
        if (Math.Abs(value - _target) < 1e-9f && _remaining == 0) return;
        _target = value;
        _remaining = _rampSamples;
        _step = (_target - _current) / _rampSamples;
    }

    public void SetImmediate(float value)
    {
        _current = _target = value;
        _remaining = 0;
    }

    public float Next()
    {
        if (_remaining > 0)
        {
            _current += _step;
            if (--_remaining == 0) _current = _target;
        }
        return _current;
    }
}

/// <summary>Circular delay line with fractional (Hermite-interpolated) reads.</summary>
public sealed class DelayLine
{
    private readonly float[] _buf;
    private readonly int _mask;
    private int _write;

    public DelayLine(int maxSamples)
    {
        int size = 1;
        while (size < maxSamples + 4) size <<= 1;
        _buf = new float[size];
        _mask = size - 1;
    }

    public int Capacity => _buf.Length - 4;

    public void Clear()
    {
        Array.Clear(_buf);
        _write = 0;
    }

    public void Write(float v)
    {
        _buf[_write] = v;
        _write = (_write + 1) & _mask;
    }

    /// <summary>Reads a sample delayed by an integer number of samples (>= 1).</summary>
    public float Read(int delay)
    {
        return _buf[(_write - delay) & _mask];
    }

    /// <summary>Reads a sample delayed by a fractional number of samples (>= 2).</summary>
    public float ReadFractional(float delay)
    {
        int d = (int)delay;
        float t = delay - d;
        int i0 = (_write - d) & _mask;
        float xm1 = _buf[(i0 + 1) & _mask];
        float x0 = _buf[i0];
        float x1 = _buf[(i0 - 1) & _mask];
        float x2 = _buf[(i0 - 2) & _mask];
        return DspUtil.Hermite(xm1, x0, x1, x2, t);
    }
}

/// <summary>Simple growable FIFO of floats used by block-based stream processors.</summary>
public sealed class FloatFifo
{
    private float[] _buf;
    private int _start, _count;

    public FloatFifo(int capacity = 8192) => _buf = new float[capacity];

    public int Count => _count;

    public void Clear() { _start = 0; _count = 0; }

    public void Write(ReadOnlySpan<float> data)
    {
        EnsureSpace(data.Length);
        data.CopyTo(_buf.AsSpan(_start + _count, data.Length));
        _count += data.Length;
    }

    public void WriteZeros(int n)
    {
        EnsureSpace(n);
        Array.Clear(_buf, _start + _count, n);
        _count += n;
    }

    public ReadOnlySpan<float> Peek(int n) => _buf.AsSpan(_start, Math.Min(n, _count));

    /// <summary>Direct access to the underlying array and read offset (valid until the next write).</summary>
    public float[] Buffer => _buf;
    public int Offset => _start;

    public int Read(Span<float> dest)
    {
        int n = Math.Min(dest.Length, _count);
        _buf.AsSpan(_start, n).CopyTo(dest);
        Consume(n);
        return n;
    }

    public void Consume(int n)
    {
        n = Math.Min(n, _count);
        _start += n;
        _count -= n;
        if (_count == 0) _start = 0;
    }

    public void Discard(int n) => Consume(n);

    private void EnsureSpace(int n)
    {
        if (_start + _count + n <= _buf.Length) return;
        if (_count + n <= _buf.Length)
        {
            Array.Copy(_buf, _start, _buf, 0, _count);
            _start = 0;
            return;
        }
        int size = _buf.Length;
        while (size < _count + n) size <<= 1;
        var nb = new float[size];
        Array.Copy(_buf, _start, nb, 0, _count);
        _buf = nb;
        _start = 0;
    }
}
