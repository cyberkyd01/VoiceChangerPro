namespace VoiceChanger.Core.Dsp;

/// <summary>Vocal tremor: LFO-driven pitch vibrato (modulated delay) plus amplitude modulation.</summary>
public sealed class Tremor
{
    private readonly float _fs;
    private readonly DelayLine _delay;
    private readonly SmoothedValue _depthSamples;
    private readonly SmoothedValue _ampDepth;
    private double _phase;
    private double _phaseInc;
    private const int MaxDepth = 1400;

    public Tremor(float sampleRate)
    {
        _fs = sampleRate;
        _delay = new DelayLine(MaxDepth * 2 + 16);
        _depthSamples = new SmoothedValue(0, sampleRate, 80);
        _ampDepth = new SmoothedValue(0, sampleRate, 40);
        Configure(5.5f, 0, 0);
    }

    public bool Active => _depthSamples.Target > 0.01f || _ampDepth.Target > 0.001f || _depthSamples.IsSmoothing;

    public void Configure(float rateHz, float pitchDepthSemitones, float ampDepth)
    {
        rateHz = Math.Clamp(rateHz, 0.1f, 20f);
        _phaseInc = 2 * Math.PI * rateHz / _fs;
        // delay modulation amplitude that yields ±depth semitones of pitch deviation
        double a = (pitchDepthSemitones * Math.Log(2) / 12.0) / (2 * Math.PI * rateHz) * _fs;
        _depthSamples.SetTarget((float)Math.Min(a, MaxDepth));
        _ampDepth.SetTarget(ampDepth);
    }

    public void Reset() { _delay.Clear(); _phase = 0; }

    public void Process(Span<float> buf)
    {
        if (!Active)
        {
            // keep the delay line primed so switching on later is click-free
            for (int i = 0; i < buf.Length; i++) _delay.Write(buf[i]);
            return;
        }
        for (int i = 0; i < buf.Length; i++)
        {
            float a = _depthSamples.Next();
            float amp = _ampDepth.Next();
            _delay.Write(buf[i]);
            double s = Math.Sin(_phase);
            float d = 3f + a + a * (float)s;
            float y = _delay.ReadFractional(d);
            float g = 1f - amp * 0.8f * 0.5f * (1f + (float)Math.Sin(_phase + 0.9));
            buf[i] = y * g;
            _phase += _phaseInc;
            if (_phase > 2 * Math.PI) _phase -= 2 * Math.PI;
        }
    }
}

/// <summary>Soft saturation with slight asymmetry for gravel / rasp, DC-blocked.</summary>
public sealed class Saturator
{
    private readonly SmoothedValue _amount;
    private readonly Biquad _dc;

    public Saturator(float sampleRate)
    {
        _amount = new SmoothedValue(0, sampleRate, 30);
        _dc = new Biquad(sampleRate, BiquadType.HighPass, 20, 0.7071);
    }

    public void Configure(float rasp) => _amount.SetTarget(rasp);
    public void Reset() => _dc.Reset();

    public void Process(Span<float> buf)
    {
        if (_amount.Target <= 0.0005f && !_amount.IsSmoothing) return;
        for (int i = 0; i < buf.Length; i++)
        {
            float r = _amount.Next();
            float x = buf[i];
            float drive = 1f + r * 14f;
            float comp = 1f / MathF.Sqrt(drive);
            float shaped = MathF.Tanh(x * drive + 0.2f * r * x * x * drive) * comp;
            buf[i] = x * (1f - r) + shaped * r * 1.6f;
        }
        _dc.Process(buf);
    }
}

/// <summary>Nasal coloration: resonant peak near 1 kHz, presence peak near 2.6 kHz and a dip around 500 Hz.</summary>
public sealed class Nasality
{
    private readonly Biquad _peak, _peak2, _dip;
    private float _amount;

    public Nasality(float sampleRate)
    {
        _peak = new Biquad(sampleRate, BiquadType.Peaking, 1000, 2.5, 0);
        _peak2 = new Biquad(sampleRate, BiquadType.Peaking, 2600, 2.0, 0);
        _dip = new Biquad(sampleRate, BiquadType.Peaking, 500, 1.5, 0);
    }

    public void Configure(float amount)
    {
        _amount = amount;
        _peak.Configure(BiquadType.Peaking, 1000, 2.5, amount * 12);
        _peak2.Configure(BiquadType.Peaking, 2600, 2.0, amount * 6);
        _dip.Configure(BiquadType.Peaking, 500, 1.5, -amount * 8);
    }

    public void Reset() { _peak.Reset(); _peak2.Reset(); _dip.Reset(); }

    public void Process(Span<float> buf)
    {
        if (_amount <= 0.001f) return;
        _peak.Process(buf);
        _peak2.Process(buf);
        _dip.Process(buf);
    }
}

/// <summary>Ring modulator ("robot").</summary>
public sealed class RingModulator
{
    private readonly float _fs;
    private readonly SmoothedValue _mix;
    private double _phase, _inc;

    public RingModulator(float sampleRate)
    {
        _fs = sampleRate;
        _mix = new SmoothedValue(0, sampleRate, 30);
    }

    public void Configure(float freqHz, float mix)
    {
        _inc = 2 * Math.PI * freqHz / _fs;
        _mix.SetTarget(mix);
    }

    public void Reset() => _phase = 0;

    public void Process(Span<float> buf)
    {
        if (_mix.Target <= 0.0005f && !_mix.IsSmoothing) return;
        for (int i = 0; i < buf.Length; i++)
        {
            float m = _mix.Next();
            float x = buf[i];
            float y = x * (float)Math.Sin(_phase);
            buf[i] = x * (1f - m) + y * m;
            _phase += _inc;
            if (_phase > 2 * Math.PI) _phase -= 2 * Math.PI;
        }
    }
}

/// <summary>Two-voice chorus.</summary>
public sealed class Chorus
{
    private readonly float _fs;
    private readonly DelayLine _delay;
    private readonly SmoothedValue _mix;
    private double _phase, _inc;
    private float _depthSamples;
    private readonly float _baseDelay;

    public Chorus(float sampleRate)
    {
        _fs = sampleRate;
        _baseDelay = sampleRate * 0.012f;
        _delay = new DelayLine((int)(sampleRate * 0.04f) + 16);
        _mix = new SmoothedValue(0, sampleRate, 30);
    }

    public void Configure(float rateHz, float depthMs, float mix)
    {
        _inc = 2 * Math.PI * rateHz / _fs;
        _depthSamples = Math.Min(depthMs * 0.001f * _fs, _baseDelay - 4);
        _mix.SetTarget(mix);
    }

    public void Reset() { _delay.Clear(); _phase = 0; }

    public void Process(Span<float> buf)
    {
        if (_mix.Target <= 0.0005f && !_mix.IsSmoothing)
        {
            for (int i = 0; i < buf.Length; i++) _delay.Write(buf[i]);
            return;
        }
        for (int i = 0; i < buf.Length; i++)
        {
            float m = _mix.Next();
            float x = buf[i];
            _delay.Write(x);
            float d1 = _baseDelay + _depthSamples * (float)Math.Sin(_phase);
            float d2 = _baseDelay * 0.8f + _depthSamples * (float)Math.Sin(_phase * 1.31 + 2.1);
            float wet = (_delay.ReadFractional(d1) + _delay.ReadFractional(d2)) * 0.5f;
            buf[i] = x * (1f - 0.5f * m) + wet * m;
            _phase += _inc;
            if (_phase > 2 * Math.PI) _phase -= 2 * Math.PI;
        }
    }
}

/// <summary>Feedback echo with damped repeats.</summary>
public sealed class Echo
{
    private readonly float _fs;
    private readonly DelayLine _delay;
    private readonly SmoothedValue _mix, _delaySamples;
    private float _feedback;
    private float _lp;
    private readonly float _lpCoef;

    public Echo(float sampleRate)
    {
        _fs = sampleRate;
        _delay = new DelayLine((int)(sampleRate * 1.6f));
        _mix = new SmoothedValue(0, sampleRate, 30);
        _delaySamples = new SmoothedValue(sampleRate * 0.25f, sampleRate, 120);
        _lpCoef = DspUtil.TimeConstant(0.08f, sampleRate); // ~2 kHz-ish damping
    }

    public void Configure(float delayMs, float feedback, float mix)
    {
        _delaySamples.SetTarget(Math.Min(delayMs * 0.001f * _fs, _delay.Capacity - 4));
        _feedback = feedback;
        _mix.SetTarget(mix);
    }

    public void Reset() { _delay.Clear(); _lp = 0; }

    public void Process(Span<float> buf)
    {
        if (_mix.Target <= 0.0005f && !_mix.IsSmoothing)
        {
            for (int i = 0; i < buf.Length; i++) _delay.Write(buf[i]);
            return;
        }
        for (int i = 0; i < buf.Length; i++)
        {
            float m = _mix.Next();
            float d = _delaySamples.Next();
            float x = buf[i];
            float tap = _delay.ReadFractional(d);
            _lp += _lpCoef * (tap - _lp);
            _delay.Write(x + _lp * _feedback);
            buf[i] = x * (1f - 0.4f * m) + tap * m;
        }
    }
}

/// <summary>Freeverb-style mono reverb with pre-delay.</summary>
public sealed class Reverb
{
    private static readonly int[] CombTunings = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
    private static readonly int[] AllpassTunings = { 556, 441, 341, 225 };

    private readonly float[][] _comb;
    private readonly int[] _combIdx;
    private readonly float[] _combStore;
    private readonly float[][] _allpass;
    private readonly int[] _allIdx;
    private readonly DelayLine _preDelay;
    private readonly SmoothedValue _mix, _preDelaySamples;
    private readonly float _fs;
    private float _feedback = 0.84f, _damp = 0.5f;

    public Reverb(float sampleRate)
    {
        _fs = sampleRate;
        float scale = sampleRate / 44100f;
        _comb = new float[8][];
        _combIdx = new int[8];
        _combStore = new float[8];
        for (int i = 0; i < 8; i++) _comb[i] = new float[Math.Max(4, (int)(CombTunings[i] * scale))];
        _allpass = new float[4][];
        _allIdx = new int[4];
        for (int i = 0; i < 4; i++) _allpass[i] = new float[Math.Max(4, (int)(AllpassTunings[i] * scale))];
        _preDelay = new DelayLine((int)(sampleRate * 0.25f));
        _mix = new SmoothedValue(0, sampleRate, 40);
        _preDelaySamples = new SmoothedValue(sampleRate * 0.01f, sampleRate, 100);
    }

    public void Configure(float size, float damping, float preDelayMs, float mix)
    {
        _feedback = 0.70f + size * 0.28f;
        _damp = damping * 0.6f;
        _preDelaySamples.SetTarget(Math.Max(2f, preDelayMs * 0.001f * _fs));
        _mix.SetTarget(mix);
    }

    public void Reset()
    {
        foreach (var c in _comb) Array.Clear(c);
        foreach (var a in _allpass) Array.Clear(a);
        Array.Clear(_combStore);
        Array.Clear(_combIdx);
        Array.Clear(_allIdx);
        _preDelay.Clear();
    }

    public void Process(Span<float> buf)
    {
        if (_mix.Target <= 0.0005f && !_mix.IsSmoothing)
        {
            for (int i = 0; i < buf.Length; i++) _preDelay.Write(buf[i]);
            return;
        }
        for (int i = 0; i < buf.Length; i++)
        {
            float m = _mix.Next();
            float x = buf[i];
            _preDelay.Write(x);
            float input = _preDelay.ReadFractional(_preDelaySamples.Next()) * 0.015f;
            float acc = 0;
            for (int c = 0; c < 8; c++)
            {
                var b = _comb[c];
                int idx = _combIdx[c];
                float outp = b[idx];
                _combStore[c] = outp * (1f - _damp) + _combStore[c] * _damp;
                b[idx] = input + _combStore[c] * _feedback;
                if (++idx >= b.Length) idx = 0;
                _combIdx[c] = idx;
                acc += outp;
            }
            for (int a = 0; a < 4; a++)
            {
                var b = _allpass[a];
                int idx = _allIdx[a];
                float bufout = b[idx];
                float outp = -acc + bufout;
                b[idx] = acc + bufout * 0.5f;
                if (++idx >= b.Length) idx = 0;
                _allIdx[a] = idx;
                acc = outp;
            }
            float wet = acc * 3f;
            buf[i] = x * (1f - 0.5f * m) + wet * m;
        }
    }
}

/// <summary>Band-limited radio / telephone / megaphone effect with drive.</summary>
public sealed class RadioEffect
{
    private readonly Biquad _hp1, _hp2, _lp1, _lp2;
    private readonly SmoothedValue _mix;
    private float _drive;

    public RadioEffect(float sampleRate)
    {
        _hp1 = new Biquad(sampleRate, BiquadType.HighPass, 300, 0.54);
        _hp2 = new Biquad(sampleRate, BiquadType.HighPass, 300, 1.31);
        _lp1 = new Biquad(sampleRate, BiquadType.LowPass, 3400, 0.54);
        _lp2 = new Biquad(sampleRate, BiquadType.LowPass, 3400, 1.31);
        _mix = new SmoothedValue(0, sampleRate, 30);
    }

    public void Configure(float lowHz, float highHz, float drive, float mix)
    {
        _hp1.Configure(BiquadType.HighPass, lowHz, 0.54);
        _hp2.Configure(BiquadType.HighPass, lowHz, 1.31);
        _lp1.Configure(BiquadType.LowPass, highHz, 0.54);
        _lp2.Configure(BiquadType.LowPass, highHz, 1.31);
        _drive = drive;
        _mix.SetTarget(mix);
    }

    public void Reset() { _hp1.Reset(); _hp2.Reset(); _lp1.Reset(); _lp2.Reset(); }

    public void Process(Span<float> buf)
    {
        if (_mix.Target <= 0.0005f && !_mix.IsSmoothing) return;
        Span<float> wet = stackalloc float[buf.Length];
        buf.CopyTo(wet);
        _hp1.Process(wet); _hp2.Process(wet);
        _lp1.Process(wet); _lp2.Process(wet);
        float drive = 1f + _drive * 20f;
        float comp = 1f / MathF.Pow(drive, 0.55f);
        for (int i = 0; i < buf.Length; i++)
        {
            float m = _mix.Next();
            float w = _drive > 0.001f ? MathF.Tanh(wet[i] * drive) * comp * 1.3f : wet[i];
            buf[i] = buf[i] * (1f - m) + w * m;
        }
    }
}

/// <summary>Bit-depth and sample-rate reduction.</summary>
public sealed class Crusher
{
    private readonly SmoothedValue _mix;
    private float _amount;
    private float _hold;
    private float _phase;

    public Crusher(float sampleRate)
    {
        _mix = new SmoothedValue(0, sampleRate, 30);
    }

    public void Configure(float amount)
    {
        _amount = amount;
        _mix.SetTarget(Math.Min(1f, amount * 2f));
    }

    public void Reset() { _hold = 0; _phase = 0; }

    public void Process(Span<float> buf)
    {
        if (_mix.Target <= 0.0005f && !_mix.IsSmoothing) return;
        float bits = 16f - _amount * 12f;
        float steps = MathF.Pow(2f, bits - 1);
        float factor = 1f + _amount * 15f;
        for (int i = 0; i < buf.Length; i++)
        {
            float m = _mix.Next();
            _phase += 1f;
            if (_phase >= factor)
            {
                _phase -= factor;
                _hold = MathF.Round(buf[i] * steps) / steps;
            }
            buf[i] = buf[i] * (1f - m) + _hold * m;
        }
    }
}
