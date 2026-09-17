using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace VoiceChanger.Core.Presets;

/// <summary>
/// Complete, serializable description of a voice transformation.
/// Every DSP stage of the pipeline reads its parameters from this object.
/// Implements INotifyPropertyChanged so the UI can bind to it directly; the engine
/// snapshots (clones) it whenever it changes and applies it on the audio thread.
/// </summary>
public sealed class VoiceProfile : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _suppressNotifications;

    // ───────────────────────────── Input ─────────────────────────────
    private float _inputGainDb;
    /// <summary>Pre-gain applied to the raw microphone signal. Range -24..+24 dB.</summary>
    public float InputGainDb { get => _inputGainDb; set => Set(ref _inputGainDb, Clamp(value, -24, 24)); }

    private float _highPassHz = 80;
    /// <summary>Rumble / plosive filter cutoff. Range 20..400 Hz.</summary>
    public float HighPassHz { get => _highPassHz; set => Set(ref _highPassHz, Clamp(value, 20, 400)); }

    // ───────────────────────────── Noise gate ─────────────────────────────
    private bool _gateEnabled = true;
    public bool GateEnabled { get => _gateEnabled; set => Set(ref _gateEnabled, value); }

    private float _gateThresholdDb = -48;
    /// <summary>Level below which the gate closes. Range -80..0 dB.</summary>
    public float GateThresholdDb { get => _gateThresholdDb; set => Set(ref _gateThresholdDb, Clamp(value, -80, 0)); }

    private float _gateAttackMs = 2;
    public float GateAttackMs { get => _gateAttackMs; set => Set(ref _gateAttackMs, Clamp(value, 0.1f, 100)); }

    private float _gateReleaseMs = 150;
    public float GateReleaseMs { get => _gateReleaseMs; set => Set(ref _gateReleaseMs, Clamp(value, 5, 2000)); }

    private float _gateHoldMs = 80;
    public float GateHoldMs { get => _gateHoldMs; set => Set(ref _gateHoldMs, Clamp(value, 0, 1000)); }

    // ───────────────────────────── Voice character ─────────────────────────────
    private float _pitchSemitones;
    /// <summary>Pitch shift in semitones (fractional allowed). Range -24..+24.</summary>
    public float PitchSemitones { get => _pitchSemitones; set => Set(ref _pitchSemitones, Clamp(value, -24, 24)); }

    private float _formantSemitones;
    /// <summary>
    /// Formant (vocal-tract size) shift in semitones, independent of pitch.
    /// Positive = smaller head/throat (child, female), negative = larger (big man, giant). Range -12..+12.
    /// </summary>
    public float FormantSemitones { get => _formantSemitones; set => Set(ref _formantSemitones, Clamp(value, -12, 12)); }

    private float _tremorRateHz = 5.5f;
    /// <summary>Vocal tremor / vibrato rate. Range 0.5..12 Hz.</summary>
    public float TremorRateHz { get => _tremorRateHz; set => Set(ref _tremorRateHz, Clamp(value, 0.5f, 12)); }

    private float _tremorPitchDepth;
    /// <summary>Vocal tremor pitch depth in semitones (peak). Range 0..1.</summary>
    public float TremorPitchDepth { get => _tremorPitchDepth; set => Set(ref _tremorPitchDepth, Clamp(value, 0, 1)); }

    private float _tremorAmpDepth;
    /// <summary>Vocal tremor amplitude depth. Range 0..1.</summary>
    public float TremorAmpDepth { get => _tremorAmpDepth; set => Set(ref _tremorAmpDepth, Clamp(value, 0, 1)); }

    private float _breathiness;
    /// <summary>Adds envelope-shaped aspiration noise (breathy / whispery / aged voice). Range 0..1.</summary>
    public float Breathiness { get => _breathiness; set => Set(ref _breathiness, Clamp(value, 0, 1)); }

    private float _rasp;
    /// <summary>Harmonic saturation / gravel. Range 0..1.</summary>
    public float Rasp { get => _rasp; set => Set(ref _rasp, Clamp(value, 0, 1)); }

    private float _nasality;
    /// <summary>Resonant nasal peak around 1 kHz plus a 500 Hz dip (nerdy / congested voice). Range 0..1.</summary>
    public float Nasality { get => _nasality; set => Set(ref _nasality, Clamp(value, 0, 1)); }

    // ───────────────────────────── Equalizer ─────────────────────────────
    private bool _eqEnabled = true;
    public bool EqEnabled { get => _eqEnabled; set => Set(ref _eqEnabled, value); }

    private float _lowShelfHz = 150;
    public float LowShelfHz { get => _lowShelfHz; set => Set(ref _lowShelfHz, Clamp(value, 40, 600)); }
    private float _lowShelfDb;
    public float LowShelfDb { get => _lowShelfDb; set => Set(ref _lowShelfDb, Clamp(value, -18, 18)); }

    private float _peak1Hz = 300;
    public float Peak1Hz { get => _peak1Hz; set => Set(ref _peak1Hz, Clamp(value, 80, 1200)); }
    private float _peak1Db;
    public float Peak1Db { get => _peak1Db; set => Set(ref _peak1Db, Clamp(value, -18, 18)); }
    private float _peak1Q = 1.0f;
    public float Peak1Q { get => _peak1Q; set => Set(ref _peak1Q, Clamp(value, 0.2f, 10)); }

    private float _peak2Hz = 1000;
    public float Peak2Hz { get => _peak2Hz; set => Set(ref _peak2Hz, Clamp(value, 300, 4000)); }
    private float _peak2Db;
    public float Peak2Db { get => _peak2Db; set => Set(ref _peak2Db, Clamp(value, -18, 18)); }
    private float _peak2Q = 1.0f;
    public float Peak2Q { get => _peak2Q; set => Set(ref _peak2Q, Clamp(value, 0.2f, 10)); }

    private float _peak3Hz = 3000;
    public float Peak3Hz { get => _peak3Hz; set => Set(ref _peak3Hz, Clamp(value, 1000, 8000)); }
    private float _peak3Db;
    public float Peak3Db { get => _peak3Db; set => Set(ref _peak3Db, Clamp(value, -18, 18)); }
    private float _peak3Q = 1.0f;
    public float Peak3Q { get => _peak3Q; set => Set(ref _peak3Q, Clamp(value, 0.2f, 10)); }

    private float _peak4Hz = 6000;
    public float Peak4Hz { get => _peak4Hz; set => Set(ref _peak4Hz, Clamp(value, 2000, 16000)); }
    private float _peak4Db;
    public float Peak4Db { get => _peak4Db; set => Set(ref _peak4Db, Clamp(value, -18, 18)); }
    private float _peak4Q = 1.0f;
    public float Peak4Q { get => _peak4Q; set => Set(ref _peak4Q, Clamp(value, 0.2f, 10)); }

    private float _highShelfHz = 8000;
    public float HighShelfHz { get => _highShelfHz; set => Set(ref _highShelfHz, Clamp(value, 2000, 16000)); }
    private float _highShelfDb;
    public float HighShelfDb { get => _highShelfDb; set => Set(ref _highShelfDb, Clamp(value, -18, 18)); }

    private float _lowPassHz = 20000;
    /// <summary>Low-pass cutoff. 20000 = effectively off. Range 1000..20000 Hz.</summary>
    public float LowPassHz { get => _lowPassHz; set => Set(ref _lowPassHz, Clamp(value, 1000, 20000)); }

    // ───────────────────────────── De-esser ─────────────────────────────
    private bool _deEsserEnabled;
    public bool DeEsserEnabled { get => _deEsserEnabled; set => Set(ref _deEsserEnabled, value); }
    private float _deEsserHz = 6500;
    public float DeEsserHz { get => _deEsserHz; set => Set(ref _deEsserHz, Clamp(value, 3000, 12000)); }
    private float _deEsserThresholdDb = -30;
    public float DeEsserThresholdDb { get => _deEsserThresholdDb; set => Set(ref _deEsserThresholdDb, Clamp(value, -60, 0)); }
    private float _deEsserAmount = 0.5f;
    public float DeEsserAmount { get => _deEsserAmount; set => Set(ref _deEsserAmount, Clamp(value, 0, 1)); }

    // ───────────────────────────── Compressor ─────────────────────────────
    private bool _compressorEnabled = true;
    public bool CompressorEnabled { get => _compressorEnabled; set => Set(ref _compressorEnabled, value); }
    private float _compThresholdDb = -20;
    public float CompThresholdDb { get => _compThresholdDb; set => Set(ref _compThresholdDb, Clamp(value, -60, 0)); }
    private float _compRatio = 3;
    public float CompRatio { get => _compRatio; set => Set(ref _compRatio, Clamp(value, 1, 20)); }
    private float _compAttackMs = 8;
    public float CompAttackMs { get => _compAttackMs; set => Set(ref _compAttackMs, Clamp(value, 0.1f, 200)); }
    private float _compReleaseMs = 120;
    public float CompReleaseMs { get => _compReleaseMs; set => Set(ref _compReleaseMs, Clamp(value, 10, 2000)); }
    private float _compKneeDb = 6;
    public float CompKneeDb { get => _compKneeDb; set => Set(ref _compKneeDb, Clamp(value, 0, 24)); }
    private float _compMakeupDb = 2;
    public float CompMakeupDb { get => _compMakeupDb; set => Set(ref _compMakeupDb, Clamp(value, -12, 24)); }

    // ───────────────────────────── Creative effects ─────────────────────────────
    private float _robotMix;
    /// <summary>Ring-modulation ("robot") wet mix. Range 0..1.</summary>
    public float RobotMix { get => _robotMix; set => Set(ref _robotMix, Clamp(value, 0, 1)); }
    private float _robotFrequencyHz = 60;
    public float RobotFrequencyHz { get => _robotFrequencyHz; set => Set(ref _robotFrequencyHz, Clamp(value, 5, 2000)); }

    private float _chorusMix;
    public float ChorusMix { get => _chorusMix; set => Set(ref _chorusMix, Clamp(value, 0, 1)); }
    private float _chorusRateHz = 0.8f;
    public float ChorusRateHz { get => _chorusRateHz; set => Set(ref _chorusRateHz, Clamp(value, 0.05f, 8)); }
    private float _chorusDepthMs = 3;
    public float ChorusDepthMs { get => _chorusDepthMs; set => Set(ref _chorusDepthMs, Clamp(value, 0.2f, 15)); }

    private float _echoMix;
    public float EchoMix { get => _echoMix; set => Set(ref _echoMix, Clamp(value, 0, 1)); }
    private float _echoDelayMs = 250;
    public float EchoDelayMs { get => _echoDelayMs; set => Set(ref _echoDelayMs, Clamp(value, 20, 1500)); }
    private float _echoFeedback = 0.3f;
    public float EchoFeedback { get => _echoFeedback; set => Set(ref _echoFeedback, Clamp(value, 0, 0.95f)); }

    private float _reverbMix;
    public float ReverbMix { get => _reverbMix; set => Set(ref _reverbMix, Clamp(value, 0, 1)); }
    private float _reverbSize = 0.5f;
    public float ReverbSize { get => _reverbSize; set => Set(ref _reverbSize, Clamp(value, 0, 1)); }
    private float _reverbDamping = 0.5f;
    public float ReverbDamping { get => _reverbDamping; set => Set(ref _reverbDamping, Clamp(value, 0, 1)); }
    private float _reverbPreDelayMs = 10;
    public float ReverbPreDelayMs { get => _reverbPreDelayMs; set => Set(ref _reverbPreDelayMs, Clamp(value, 0, 200)); }

    private float _radioMix;
    /// <summary>Band-limited "telephone / radio / megaphone" wet mix. Range 0..1.</summary>
    public float RadioMix { get => _radioMix; set => Set(ref _radioMix, Clamp(value, 0, 1)); }
    private float _radioLowHz = 300;
    public float RadioLowHz { get => _radioLowHz; set => Set(ref _radioLowHz, Clamp(value, 50, 2000)); }
    private float _radioHighHz = 3400;
    public float RadioHighHz { get => _radioHighHz; set => Set(ref _radioHighHz, Clamp(value, 1000, 12000)); }
    private float _radioDrive;
    public float RadioDrive { get => _radioDrive; set => Set(ref _radioDrive, Clamp(value, 0, 1)); }

    private float _crush;
    /// <summary>Bit-depth / sample-rate reduction (lo-fi, walkie-talkie, retro game). Range 0..1.</summary>
    public float Crush { get => _crush; set => Set(ref _crush, Clamp(value, 0, 1)); }

    // ───────────────────────────── Output ─────────────────────────────
    private float _outputGainDb;
    public float OutputGainDb { get => _outputGainDb; set => Set(ref _outputGainDb, Clamp(value, -24, 24)); }
    private bool _limiterEnabled = true;
    public bool LimiterEnabled { get => _limiterEnabled; set => Set(ref _limiterEnabled, value); }
    private float _limiterCeilingDb = -1;
    public float LimiterCeilingDb { get => _limiterCeilingDb; set => Set(ref _limiterCeilingDb, Clamp(value, -12, 0)); }

    // ───────────────────────────── Helpers ─────────────────────────────
    private static float Clamp(float v, float min, float max)
    {
        if (float.IsNaN(v)) return min;
        return v < min ? min : v > max ? max : v;
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        if (!_suppressNotifications)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>Creates an independent deep copy.</summary>
    public VoiceProfile Clone()
    {
        var c = new VoiceProfile();
        c.CopyFrom(this, notify: false);
        return c;
    }

    /// <summary>Copies all parameter values from another profile, raising one aggregate change notification.</summary>
    public void CopyFrom(VoiceProfile other, bool notify = true)
    {
        _suppressNotifications = true;
        try
        {
            foreach (var p in Properties)
                p.SetValue(this, p.GetValue(other));
        }
        finally
        {
            _suppressNotifications = false;
        }
        if (notify)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    /// <summary>Resets to a neutral, transparent profile (clean microphone).</summary>
    public void Reset() => CopyFrom(new VoiceProfile());

    public bool ValueEquals(VoiceProfile other)
    {
        foreach (var p in Properties)
            if (!Equals(p.GetValue(this), p.GetValue(other))) return false;
        return true;
    }

    [JsonIgnore]
    public bool IsNeutral => ValueEquals(new VoiceProfile());

    private static readonly System.Reflection.PropertyInfo[] Properties = typeof(VoiceProfile)
        .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
        .Where(p => p.CanRead && p.CanWrite && p.GetCustomAttributes(typeof(JsonIgnoreAttribute), true).Length == 0)
        .ToArray();
}
