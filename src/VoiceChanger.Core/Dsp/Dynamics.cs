namespace VoiceChanger.Core.Dsp;

/// <summary>
/// Downward noise gate with hysteresis, hold time and smoothed gain.
/// </summary>
public sealed class NoiseGate
{
    private readonly float _fs;
    private float _env;          // fast peak envelope (linear)
    private float _gain;         // current gain 0..1
    private int _holdCounter;
    private bool _open;

    private float _thresholdLin = DspUtil.DbToLinear(-48);
    private float _hysteresisLin = DspUtil.DbToLinear(-54);
    private float _attackCoef, _releaseCoef;
    private int _holdSamples;
    private readonly float _envAttack, _envRelease;

    public bool Enabled { get; set; } = true;

    public NoiseGate(float sampleRate)
    {
        _fs = sampleRate;
        _envAttack = DspUtil.TimeConstant(0.5f, sampleRate);
        _envRelease = DspUtil.TimeConstant(40f, sampleRate);
        Configure(-48, 2, 150, 80);
    }

    public void Configure(float thresholdDb, float attackMs, float releaseMs, float holdMs)
    {
        _thresholdLin = DspUtil.DbToLinear(thresholdDb);
        _hysteresisLin = DspUtil.DbToLinear(thresholdDb - 6f);
        _attackCoef = DspUtil.TimeConstant(attackMs, _fs);
        _releaseCoef = DspUtil.TimeConstant(releaseMs, _fs);
        _holdSamples = (int)(holdMs * 0.001f * _fs);
    }

    /// <summary>True while the gate is passing signal (for UI indication).</summary>
    public bool IsOpen => _open;

    public void Reset()
    {
        _env = 0; _gain = 0; _holdCounter = 0; _open = false;
    }

    public void Process(Span<float> buf)
    {
        if (!Enabled)
        {
            _gain = 1; _open = true;
            return;
        }

        for (int i = 0; i < buf.Length; i++)
        {
            float x = buf[i];
            float a = MathF.Abs(x);
            _env += (a > _env ? _envAttack : _envRelease) * (a - _env);

            if (_open)
            {
                if (_env < _hysteresisLin)
                {
                    if (_holdCounter > 0) _holdCounter--;
                    else _open = false;
                }
                else _holdCounter = _holdSamples;
            }
            else if (_env > _thresholdLin)
            {
                _open = true;
                _holdCounter = _holdSamples;
            }

            float target = _open ? 1f : 0f;
            _gain += (target > _gain ? _attackCoef : _releaseCoef) * (target - _gain);
            buf[i] = x * _gain;
        }
    }
}

/// <summary>
/// Feed-forward peak compressor with soft knee, log-domain gain computer and
/// smoothed gain reduction. Reports gain reduction for metering.
/// </summary>
public sealed class Compressor
{
    private readonly float _fs;
    private float _thresholdDb = -20, _ratio = 3, _kneeDb = 6, _makeupLin = 1;
    private float _attackCoef, _releaseCoef;
    private float _envDb = DspUtil.MinusInfinityDb;
    private float _gainDb;
    private readonly float _detAttack, _detRelease;
    private float _detEnv;

    public bool Enabled { get; set; } = true;
    public float GainReductionDb { get; private set; }

    public Compressor(float sampleRate)
    {
        _fs = sampleRate;
        _detAttack = DspUtil.TimeConstant(0.2f, sampleRate);
        _detRelease = DspUtil.TimeConstant(30f, sampleRate);
        Configure(-20, 3, 8, 120, 6, 2);
    }

    public void Configure(float thresholdDb, float ratio, float attackMs, float releaseMs, float kneeDb, float makeupDb)
    {
        _thresholdDb = thresholdDb;
        _ratio = Math.Max(1f, ratio);
        _kneeDb = Math.Max(0f, kneeDb);
        _makeupLin = DspUtil.DbToLinear(makeupDb);
        _attackCoef = DspUtil.TimeConstant(attackMs, _fs);
        _releaseCoef = DspUtil.TimeConstant(releaseMs, _fs);
    }

    public void Reset()
    {
        _envDb = DspUtil.MinusInfinityDb; _gainDb = 0; _detEnv = 0; GainReductionDb = 0;
    }

    private float ComputeGainDb(float inDb)
    {
        float over = inDb - _thresholdDb;
        float halfKnee = _kneeDb * 0.5f;
        if (_kneeDb > 0 && over > -halfKnee && over < halfKnee)
        {
            float t = over + halfKnee;
            return (1f / _ratio - 1f) * t * t / (2f * _kneeDb);
        }
        if (over <= -halfKnee) return 0f;
        return (1f / _ratio - 1f) * over;
    }

    public void Process(Span<float> buf)
    {
        if (!Enabled)
        {
            GainReductionDb = 0;
            return;
        }

        float minGr = 0;
        for (int i = 0; i < buf.Length; i++)
        {
            float x = buf[i];
            float a = MathF.Abs(x);
            _detEnv += (a > _detEnv ? _detAttack : _detRelease) * (a - _detEnv);
            float inDb = DspUtil.LinearToDb(_detEnv);
            float targetGain = ComputeGainDb(inDb);
            // attack when gain must decrease (more reduction), release when it recovers
            _gainDb += (targetGain < _gainDb ? _attackCoef : _releaseCoef) * (targetGain - _gainDb);
            if (_gainDb < minGr) minGr = _gainDb;
            buf[i] = x * DspUtil.DbToLinear(_gainDb) * _makeupLin;
        }
        GainReductionDb = minGr;
    }
}

/// <summary>
/// Split-band de-esser: detects energy in a band around the sibilance frequency
/// and attenuates only that band when it exceeds the threshold.
/// </summary>
public sealed class DeEsser
{
    private readonly float _fs;
    private readonly Biquad _detector;
    private readonly Biquad _band;
    private float _thresholdLin = DspUtil.DbToLinear(-30);
    private float _amount = 0.5f;
    private float _env;
    private readonly float _attack, _release;
    private float _gr;

    public bool Enabled { get; set; }
    public float GainReductionDb { get; private set; }

    public DeEsser(float sampleRate)
    {
        _fs = sampleRate;
        _detector = new Biquad(sampleRate, BiquadType.BandPass, 6500, 1.2);
        _band = new Biquad(sampleRate, BiquadType.BandPass, 6500, 1.2);
        _attack = DspUtil.TimeConstant(1f, sampleRate);
        _release = DspUtil.TimeConstant(40f, sampleRate);
    }

    public void Configure(float freqHz, float thresholdDb, float amount)
    {
        _detector.Configure(BiquadType.BandPass, freqHz, 1.2);
        _band.Configure(BiquadType.BandPass, freqHz, 1.2);
        _thresholdLin = DspUtil.DbToLinear(thresholdDb);
        _amount = amount;
    }

    public void Reset() { _detector.Reset(); _band.Reset(); _env = 0; _gr = 0; }

    public void Process(Span<float> buf)
    {
        if (!Enabled) { GainReductionDb = 0; return; }
        float maxRed = 0;
        for (int i = 0; i < buf.Length; i++)
        {
            float x = buf[i];
            float det = MathF.Abs(_detector.Process(x));
            _env += (det > _env ? _attack : _release) * (det - _env);
            float over = _env / _thresholdLin;
            float target = over > 1f ? 1f - 1f / over : 0f; // 0..1 reduction of the band
            target *= _amount;
            _gr += (target > _gr ? _attack : _release) * (target - _gr);
            float band = _band.Process(x);
            buf[i] = x - band * _gr;
            if (_gr > maxRed) maxRed = _gr;
        }
        GainReductionDb = maxRed > 0 ? DspUtil.LinearToDb(1f - maxRed * 0.99f) : 0;
    }
}

/// <summary>Fast brick-wall style peak limiter (instant attack, exponential release) with hard-clip safety.</summary>
public sealed class Limiter
{
    private float _ceiling = DspUtil.DbToLinear(-1);
    private float _env;
    private readonly float _release;

    public bool Enabled { get; set; } = true;
    public float GainReductionDb { get; private set; }

    public Limiter(float sampleRate)
    {
        _release = DspUtil.TimeConstant(60f, sampleRate);
    }

    public void Configure(float ceilingDb) => _ceiling = DspUtil.DbToLinear(ceilingDb);

    public void Reset() { _env = 0; GainReductionDb = 0; }

    public void Process(Span<float> buf)
    {
        if (!Enabled)
        {
            GainReductionDb = 0;
            for (int i = 0; i < buf.Length; i++) buf[i] = Math.Clamp(buf[i], -1f, 1f);
            return;
        }
        float minGain = 1f;
        for (int i = 0; i < buf.Length; i++)
        {
            float a = MathF.Abs(buf[i]);
            if (a > _env) _env = a; else _env += _release * (a - _env);
            float g = _env > _ceiling ? _ceiling / _env : 1f;
            if (g < minGain) minGain = g;
            float y = buf[i] * g;
            buf[i] = Math.Clamp(y, -_ceiling, _ceiling);
        }
        GainReductionDb = DspUtil.LinearToDb(minGain);
    }
}

/// <summary>7-band parametric EQ: low shelf, four peaks, high shelf, low-pass.</summary>
public sealed class ParametricEq
{
    private readonly Biquad _lowShelf, _p1, _p2, _p3, _p4, _highShelf, _lowPass;
    private readonly float _fs;
    private bool _lowPassActive;

    public bool Enabled { get; set; } = true;

    public ParametricEq(float sampleRate)
    {
        _fs = sampleRate;
        _lowShelf = new Biquad(sampleRate, BiquadType.LowShelf, 150, 0.7071, 0);
        _p1 = new Biquad(sampleRate, BiquadType.Peaking, 300, 1, 0);
        _p2 = new Biquad(sampleRate, BiquadType.Peaking, 1000, 1, 0);
        _p3 = new Biquad(sampleRate, BiquadType.Peaking, 3000, 1, 0);
        _p4 = new Biquad(sampleRate, BiquadType.Peaking, 6000, 1, 0);
        _highShelf = new Biquad(sampleRate, BiquadType.HighShelf, 8000, 0.7071, 0);
        _lowPass = new Biquad(sampleRate, BiquadType.LowPass, 20000, 0.7071, 0);
    }

    public void Configure(Presets.VoiceProfile p)
    {
        _lowShelf.Configure(BiquadType.LowShelf, p.LowShelfHz, 0.7071, p.LowShelfDb);
        _p1.Configure(BiquadType.Peaking, p.Peak1Hz, p.Peak1Q, p.Peak1Db);
        _p2.Configure(BiquadType.Peaking, p.Peak2Hz, p.Peak2Q, p.Peak2Db);
        _p3.Configure(BiquadType.Peaking, p.Peak3Hz, p.Peak3Q, p.Peak3Db);
        _p4.Configure(BiquadType.Peaking, p.Peak4Hz, p.Peak4Q, p.Peak4Db);
        _highShelf.Configure(BiquadType.HighShelf, p.HighShelfHz, 0.7071, p.HighShelfDb);
        _lowPassActive = p.LowPassHz < Math.Min(19500f, _fs * 0.45f);
        if (_lowPassActive) _lowPass.Configure(BiquadType.LowPass, p.LowPassHz, 0.7071, 0);
    }

    public void Reset()
    {
        _lowShelf.Reset(); _p1.Reset(); _p2.Reset(); _p3.Reset(); _p4.Reset(); _highShelf.Reset(); _lowPass.Reset();
    }

    public void Process(Span<float> buf)
    {
        if (!Enabled) return;
        _lowShelf.Process(buf);
        _p1.Process(buf);
        _p2.Process(buf);
        _p3.Process(buf);
        _p4.Process(buf);
        _highShelf.Process(buf);
        if (_lowPassActive) _lowPass.Process(buf);
    }

    /// <summary>Combined magnitude response in dB (for the UI curve).</summary>
    public double MagnitudeDb(double freq)
    {
        double db = _lowShelf.MagnitudeDb(freq) + _p1.MagnitudeDb(freq) + _p2.MagnitudeDb(freq) + _p3.MagnitudeDb(freq)
                    + _p4.MagnitudeDb(freq) + _highShelf.MagnitudeDb(freq);
        if (_lowPassActive) db += _lowPass.MagnitudeDb(freq);
        return db;
    }
}
