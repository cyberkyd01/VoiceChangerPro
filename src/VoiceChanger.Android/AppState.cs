using Android.Content;
using VoiceChanger.Core.Dsp;
using VoiceChanger.Core.Presets;
using VoiceChangerPro.AndroidApp.Diagnostics;

namespace VoiceChangerPro.AndroidApp;

[Flags]
public enum StateChange
{
    None = 0,
    Preset = 1,     // a different preset was chosen (implies Profile)
    Profile = 2,    // pitch / formant / volume changed
    Global = 4,     // noise reduction / voice isolation changed
    Effect = 8      // voice effect switched on or off
}

/// <summary>
/// The user's current voice settings: chosen preset, the working copy of its profile (with slider edits),
/// global clean-up and the voice effect switch. Saved to SharedPreferences on every change so they
/// survive restarts. Used from the UI thread only.
/// </summary>
public sealed class AppState
{
    private const string PrefsName = "voice_settings";
    private const string DefaultPresetId = "builtin-soft-warm-woman";

    private readonly ISharedPreferences _prefs;

    public AppState(Context context)
    {
        _prefs = context.GetSharedPreferences(PrefsName, FileCreationMode.Private)!;
        Presets = BuiltInPresets.All;
        ActivePreset = Presets[0];
        Load();
    }

    public event Action<StateChange>? Changed;

    public IReadOnlyList<VoicePreset> Presets { get; }
    public VoicePreset ActivePreset { get; private set; }

    /// <summary>Working profile: the active preset plus the user's slider edits. Never shared with the audio thread directly.</summary>
    public VoiceProfile Profile { get; private set; } = new();

    public float NoiseReduction { get; private set; } = 0.5f;
    public float VoiceIsolation { get; private set; }
    public bool EffectEnabled { get; private set; } = true;

    /// <summary>Increments on every change; lets the recorder reuse a rendered clip while nothing changed.</summary>
    public int Version { get; private set; }

    public GlobalProcessing Global => new(NoiseReduction, VoiceIsolation);

    /// <summary>True when the sliders no longer match the preset.</summary>
    public bool IsModified =>
        Math.Abs(Profile.PitchSemitones - ActivePreset.Profile.PitchSemitones) > 0.001f ||
        Math.Abs(Profile.FormantSemitones - ActivePreset.Profile.FormantSemitones) > 0.001f ||
        Math.Abs(Profile.OutputGainDb - ActivePreset.Profile.OutputGainDb) > 0.001f;

    public void SelectPreset(VoicePreset preset)
    {
        ActivePreset = preset;
        Profile = preset.Profile.Clone();
        Commit(StateChange.Preset | StateChange.Profile);
    }

    public void ResetToPreset()
    {
        Profile = ActivePreset.Profile.Clone();
        Commit(StateChange.Profile);
    }

    public void SetPitch(float semitones) { Profile.PitchSemitones = semitones; Commit(StateChange.Profile); }
    public void SetFormant(float semitones) { Profile.FormantSemitones = semitones; Commit(StateChange.Profile); }
    public void SetOutputGain(float db) { Profile.OutputGainDb = db; Commit(StateChange.Profile); }

    public void SetNoiseReduction(float amount) { NoiseReduction = Math.Clamp(amount, 0, 1); Commit(StateChange.Global); }
    public void SetVoiceIsolation(float amount) { VoiceIsolation = Math.Clamp(amount, 0, 1); Commit(StateChange.Global); }

    public void SetEffectEnabled(bool enabled)
    {
        if (EffectEnabled == enabled) return;
        EffectEnabled = enabled;
        Commit(StateChange.Effect);
    }

    private void Commit(StateChange change)
    {
        Version++;
        Save();
        Changed?.Invoke(change);
    }

    private void Load()
    {
        try
        {
            string id = _prefs.GetString("presetId", DefaultPresetId) ?? DefaultPresetId;
            ActivePreset = Presets.FirstOrDefault(p => p.Id == id)
                           ?? Presets.FirstOrDefault(p => p.Id == DefaultPresetId)
                           ?? Presets[0];
            Profile = ActivePreset.Profile.Clone();
            if (_prefs.Contains("pitch")) Profile.PitchSemitones = _prefs.GetFloat("pitch", Profile.PitchSemitones);
            if (_prefs.Contains("formant")) Profile.FormantSemitones = _prefs.GetFloat("formant", Profile.FormantSemitones);
            if (_prefs.Contains("outputDb")) Profile.OutputGainDb = _prefs.GetFloat("outputDb", Profile.OutputGainDb);
            NoiseReduction = Math.Clamp(_prefs.GetFloat("noiseReduction", 0.5f), 0, 1);
            VoiceIsolation = Math.Clamp(_prefs.GetFloat("voiceIsolation", 0f), 0, 1);
            EffectEnabled = _prefs.GetBoolean("effectEnabled", true);
            AppLog.Info("State", $"Loaded: preset={ActivePreset.Id} pitch={Profile.PitchSemitones} formant={Profile.FormantSemitones} " +
                                 $"out={Profile.OutputGainDb}dB nr={NoiseReduction} iso={VoiceIsolation} effect={EffectEnabled}");
        }
        catch (Exception ex)
        {
            // Corrupt or incompatible preferences: start from defaults rather than crash.
            AppLog.Error("State", "Could not load saved settings; using defaults", ex);
            ActivePreset = Presets.FirstOrDefault(p => p.Id == DefaultPresetId) ?? Presets[0];
            Profile = ActivePreset.Profile.Clone();
        }
    }

    private void Save()
    {
        try
        {
            var e = _prefs.Edit()!;
            e.PutString("presetId", ActivePreset.Id);
            e.PutFloat("pitch", Profile.PitchSemitones);
            e.PutFloat("formant", Profile.FormantSemitones);
            e.PutFloat("outputDb", Profile.OutputGainDb);
            e.PutFloat("noiseReduction", NoiseReduction);
            e.PutFloat("voiceIsolation", VoiceIsolation);
            e.PutBoolean("effectEnabled", EffectEnabled);
            e.Apply(); // asynchronous write; cheap enough to call while a slider moves
        }
        catch (Exception ex)
        {
            AppLog.Error("State", "Could not save settings", ex);
        }
    }
}
