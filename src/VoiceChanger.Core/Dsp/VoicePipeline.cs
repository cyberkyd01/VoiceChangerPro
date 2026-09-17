using VoiceChanger.Core.Presets;

namespace VoiceChanger.Core.Dsp;

/// <summary>Global (preset-independent) processing that runs before the voice chain.</summary>
public sealed record GlobalProcessing(float NoiseReduction, float VoiceIsolation)
{
    public static readonly GlobalProcessing Off = new(0, 0);
}

/// <summary>
/// The complete mono voice-processing chain. Parameter updates are handed over as immutable
/// snapshots and applied at block boundaries on the audio thread, so the UI can change any
/// value at any time without locks or glitches.
/// Order: input gain → high-pass → [global: noise suppression → voice isolation] → gate →
/// PSOLA pitch → formant/breath → tremor → rasp → nasality → EQ → de-esser → compressor →
/// robot → chorus → radio → crush → echo → reverb → output gain → limiter.
/// </summary>
public sealed class VoicePipeline
{
    public float SampleRate { get; }

    private readonly Biquad _highPass;
    private readonly NoiseSuppressor _noise;
    private readonly VoiceIsolator _isolator;
    private readonly NoiseGate _gate;
    private readonly PsolaPitchShifter _pitch;
    private readonly FormantShifter _formant;
    private readonly Tremor _tremor;
    private readonly Saturator _saturator;
    private readonly Nasality _nasality;
    private readonly ParametricEq _eq;
    private readonly DeEsser _deEsser;
    private readonly Compressor _compressor;
    private readonly RingModulator _ringMod;
    private readonly Chorus _chorus;
    private readonly RadioEffect _radio;
    private readonly Crusher _crusher;
    private readonly Echo _echo;
    private readonly Reverb _reverb;
    private readonly Limiter _limiter;
    private readonly SmoothedValue _inputGain, _outputGain;

    private VoiceProfile? _pending;
    private GlobalProcessing? _pendingGlobal;
    private VoiceProfile _current = new();
    private GlobalProcessing _currentGlobal = GlobalProcessing.Off;
    private volatile bool _bypass;

    private float _inPeak, _outPeak, _inRms;

    public VoicePipeline(float sampleRate)
    {
        SampleRate = sampleRate;
        _highPass = new Biquad(sampleRate, BiquadType.HighPass, 80, 0.7071);
        _noise = new NoiseSuppressor(sampleRate);
        _isolator = new VoiceIsolator(sampleRate);
        _gate = new NoiseGate(sampleRate);
        _pitch = new PsolaPitchShifter(sampleRate);
        _formant = new FormantShifter(sampleRate);
        _tremor = new Tremor(sampleRate);
        _saturator = new Saturator(sampleRate);
        _nasality = new Nasality(sampleRate);
        _eq = new ParametricEq(sampleRate);
        _deEsser = new DeEsser(sampleRate);
        _compressor = new Compressor(sampleRate);
        _ringMod = new RingModulator(sampleRate);
        _chorus = new Chorus(sampleRate);
        _radio = new RadioEffect(sampleRate);
        _crusher = new Crusher(sampleRate);
        _echo = new Echo(sampleRate);
        _reverb = new Reverb(sampleRate);
        _limiter = new Limiter(sampleRate);
        _inputGain = new SmoothedValue(1, sampleRate, 20);
        _outputGain = new SmoothedValue(1, sampleRate, 20);
        ApplyNow(_current);
        ApplyGlobalNow(_currentGlobal);
    }

    /// <summary>Total algorithmic latency of the chain in samples.</summary>
    public int LatencySamples => _noise.LatencySamples + _pitch.LatencySamples + _formant.LatencySamples;
    public double LatencyMs => LatencySamples * 1000.0 / SampleRate;

    /// <summary>When true the voice chain is skipped (global noise processing still runs).</summary>
    public bool Bypass { get => _bypass; set => _bypass = value; }

    public float InputPeak => _inPeak;
    public float InputRms => _inRms;
    public float OutputPeak => _outPeak;
    public float CompressorGainReductionDb => _compressor.GainReductionDb;
    public float LimiterGainReductionDb => _limiter.GainReductionDb;
    public float IsolationGainReductionDb => _isolator.GainReductionDb;
    public float NoiseFloorDb => _noise.NoiseFloorDb;
    public bool GateOpen => _gate.IsOpen;
    public float DetectedPitchHz => _pitch.DetectedPitchHz > 0 ? _pitch.DetectedPitchHz : _formant.DetectedPitchHz;
    public VoiceProfile CurrentProfile => _current;
    public GlobalProcessing CurrentGlobal => _currentGlobal;

    /// <summary>Queues a profile snapshot to be applied at the next block boundary (thread-safe).</summary>
    public void Apply(VoiceProfile profile) => Volatile.Write(ref _pending, profile.Clone());

    /// <summary>Queues new global (preset-independent) settings (thread-safe).</summary>
    public void ApplyGlobal(GlobalProcessing global) => Volatile.Write(ref _pendingGlobal, global);

    private void ApplyGlobalNow(GlobalProcessing g)
    {
        _currentGlobal = g;
        _noise.Configure(g.NoiseReduction);
        _isolator.Configure(g.VoiceIsolation);
    }

    private void ApplyNow(VoiceProfile p)
    {
        _current = p;
        _inputGain.SetTarget(DspUtil.DbToLinear(p.InputGainDb));
        _highPass.Configure(BiquadType.HighPass, p.HighPassHz, 0.7071);
        _gate.Enabled = p.GateEnabled;
        _gate.Configure(p.GateThresholdDb, p.GateAttackMs, p.GateReleaseMs, p.GateHoldMs);

        // PSOLA preserves formants, so the formant stage applies the requested size change directly.
        _pitch.SetRatio(DspUtil.SemitonesToRatio(p.PitchSemitones));
        _formant.Configure(DspUtil.SemitonesToRatio(p.FormantSemitones), p.Breathiness);
        _tremor.Configure(p.TremorRateHz, p.TremorPitchDepth, p.TremorAmpDepth);
        _saturator.Configure(p.Rasp);
        _nasality.Configure(p.Nasality);

        _eq.Enabled = p.EqEnabled;
        _eq.Configure(p);
        _deEsser.Enabled = p.DeEsserEnabled;
        _deEsser.Configure(p.DeEsserHz, p.DeEsserThresholdDb, p.DeEsserAmount);
        _compressor.Enabled = p.CompressorEnabled;
        _compressor.Configure(p.CompThresholdDb, p.CompRatio, p.CompAttackMs, p.CompReleaseMs, p.CompKneeDb, p.CompMakeupDb);

        _ringMod.Configure(p.RobotFrequencyHz, p.RobotMix);
        _chorus.Configure(p.ChorusRateHz, p.ChorusDepthMs, p.ChorusMix);
        _radio.Configure(p.RadioLowHz, p.RadioHighHz, p.RadioDrive, p.RadioMix);
        _crusher.Configure(p.Crush);
        _echo.Configure(p.EchoDelayMs, p.EchoFeedback, p.EchoMix);
        _reverb.Configure(p.ReverbSize, p.ReverbDamping, p.ReverbPreDelayMs, p.ReverbMix);

        _outputGain.SetTarget(DspUtil.DbToLinear(p.OutputGainDb));
        _limiter.Enabled = p.LimiterEnabled;
        _limiter.Configure(p.LimiterCeilingDb);
    }

    public void Reset()
    {
        _highPass.Reset(); _noise.Reset(); _isolator.Reset(); _gate.Reset(); _pitch.Reset(); _formant.Reset(); _tremor.Reset();
        _saturator.Reset(); _nasality.Reset(); _eq.Reset(); _deEsser.Reset(); _compressor.Reset();
        _ringMod.Reset(); _chorus.Reset(); _radio.Reset(); _crusher.Reset(); _echo.Reset(); _reverb.Reset();
        _limiter.Reset();
        _inPeak = _outPeak = _inRms = 0;
    }

    /// <summary>Processes one block of mono float samples in place.</summary>
    public void Process(Span<float> buf)
    {
        var pending = Interlocked.Exchange(ref _pending, null);
        if (pending != null) ApplyNow(pending);
        var pendingGlobal = Interlocked.Exchange(ref _pendingGlobal, null);
        if (pendingGlobal != null) ApplyGlobalNow(pendingGlobal);

        Meter(buf, out _inPeak, out _inRms);

        // global clean-up runs even in bypass so pass-through audio is still de-noised
        _noise.Process(buf);
        _isolator.Process(buf);

        if (_bypass)
        {
            Meter(buf, out _outPeak, out _);
            return;
        }

        for (int i = 0; i < buf.Length; i++) buf[i] *= _inputGain.Next();
        _highPass.Process(buf);
        _gate.Process(buf);
        _pitch.Process(buf);
        _formant.Process(buf);
        _tremor.Process(buf);
        _saturator.Process(buf);
        _nasality.Process(buf);
        _eq.Process(buf);
        _deEsser.Process(buf);
        _compressor.Process(buf);
        _ringMod.Process(buf);
        _chorus.Process(buf);
        _radio.Process(buf);
        _crusher.Process(buf);
        _echo.Process(buf);
        _reverb.Process(buf);
        for (int i = 0; i < buf.Length; i++) buf[i] *= _outputGain.Next();
        DspUtil.Sanitize(buf);
        _limiter.Process(buf);

        Meter(buf, out _outPeak, out _);
    }

    private static void Meter(ReadOnlySpan<float> buf, out float peak, out float rms)
    {
        float p = 0; double sq = 0;
        for (int i = 0; i < buf.Length; i++)
        {
            float a = MathF.Abs(buf[i]);
            if (a > p) p = a;
            sq += buf[i] * buf[i];
        }
        peak = p;
        rms = buf.Length > 0 ? (float)Math.Sqrt(sq / buf.Length) : 0;
    }
}
