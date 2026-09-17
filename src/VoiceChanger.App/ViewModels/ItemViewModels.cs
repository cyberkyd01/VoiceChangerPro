using CommunityToolkit.Mvvm.ComponentModel;
using VoiceChanger.Core.Audio.Engine;
using VoiceChanger.Core.Presets;

namespace VoiceChanger.App.ViewModels;

/// <summary>UI wrapper around one <see cref="VoiceEngine"/> (one physical microphone).</summary>
public sealed partial class MicrophoneItemViewModel : ObservableObject
{
    private readonly EngineManager _manager;

    public VoiceEngine Engine { get; }
    public string Id => Engine.DeviceId;
    public VoiceProfile Profile => Engine.Profile;

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private EngineState _state;
    [ObservableProperty] private string _stateText = "";
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _presetName = "";
    [ObservableProperty] private string? _lastError;
    [ObservableProperty] private bool _hasClip;
    [ObservableProperty] private bool _isRecordingClip;
    [ObservableProperty] private string _routeText = "";
    [ObservableProperty] private string _formatText = "";
    [ObservableProperty] private double _latencyMs;
    [ObservableProperty] private bool _virtualActive;
    [ObservableProperty] private bool _monitorActive;
    [ObservableProperty] private string _clipText = "";

    public MicrophoneItemViewModel(EngineManager manager, VoiceEngine engine)
    {
        _manager = manager;
        Engine = engine;
        Refresh();
    }

    public bool Active
    {
        get => Engine.Config.Active;
        set
        {
            if (value == Engine.Config.Active && (value == (Engine.State == EngineState.Running))) return;
            if (value) _manager.StartEngine(Engine); else _manager.StopEngine(Engine);
            OnPropertyChanged();
            Refresh();
        }
    }

    public bool EffectEnabled
    {
        get => Engine.EffectEnabled;
        set { Engine.EffectEnabled = value; OnPropertyChanged(); Refresh(); }
    }

    public bool MonitorEnabled
    {
        get => Engine.Config.MonitorEnabled;
        set { _manager.SetMonitoring(Engine, value); OnPropertyChanged(); Refresh(); }
    }

    public double MonitorVolume
    {
        get => Engine.Config.MonitorVolume;
        set { Engine.SetMonitorVolume((float)value); OnPropertyChanged(); }
    }

    public bool LoopClip
    {
        get => Engine.LoopClip;
        set { Engine.LoopClip = value; OnPropertyChanged(); Refresh(); }
    }

    public void Refresh()
    {
        Name = Engine.DeviceName;
        State = Engine.State;
        IsRunning = Engine.State == EngineState.Running;
        StateText = Engine.State switch
        {
            EngineState.Running => Engine.EffectEnabled && Engine.MasterEnabled ? "Live · effect on" : "Live · pass-through",
            EngineState.Starting => "Starting…",
            EngineState.Faulted => "Error",
            _ => "Inactive"
        };
        PresetName = string.IsNullOrEmpty(Engine.Config.PresetName) ? "Custom" : Engine.Config.PresetName!;
        LastError = Engine.LastError;
        HasClip = Engine.HasClip;
        IsRecordingClip = Engine.IsRecordingClip;
        VirtualActive = Engine.VirtualActive;
        MonitorActive = Engine.MonitorActive;
        LatencyMs = Engine.EstimatedLatencyMs;
        FormatText = Engine.SampleRate > 0 ? $"{Engine.SampleRate / 1000.0:0.#} kHz · {Engine.InputChannels} ch" : "";
        RouteText = Engine.State != EngineState.Running ? "Not running"
            : Engine.VirtualActive ? $"→ {Engine.VirtualOutputName}"
            : "→ no virtual cable (apps will not hear this mic)";
        ClipText = Engine.IsRecordingClip ? "Recording…" : Engine.HasClip ? $"{Engine.ClipSeconds:F1} s clip" : "No clip";
        OnPropertyChanged(nameof(Active));
        OnPropertyChanged(nameof(EffectEnabled));
        OnPropertyChanged(nameof(MonitorEnabled));
        OnPropertyChanged(nameof(MonitorVolume));
        OnPropertyChanged(nameof(LoopClip));
    }
}

/// <summary>UI wrapper around a <see cref="VoicePreset"/>.</summary>
public sealed partial class PresetItemViewModel : ObservableObject
{
    public VoicePreset Preset { get; }

    public string Id => Preset.Id;
    public string Name => Preset.Name;
    public string Icon => Preset.Icon;
    public string Category => Preset.Category;
    public string Description => Preset.Description;
    public bool IsBuiltIn => Preset.IsBuiltIn;
    public string TagsText => string.Join(" · ", Preset.Tags);
    public string Summary => BuildSummary(Preset.Profile);

    [ObservableProperty] private bool _isFavorite;
    [ObservableProperty] private bool _isApplied;

    public PresetItemViewModel(VoicePreset preset, bool isFavorite)
    {
        Preset = preset;
        _isFavorite = isFavorite;
    }

    private static string BuildSummary(VoiceProfile p)
    {
        var parts = new List<string>();
        if (Math.Abs(p.PitchSemitones) > 0.05f) parts.Add($"Pitch {p.PitchSemitones:+0.#;-0.#} st");
        if (Math.Abs(p.FormantSemitones) > 0.05f) parts.Add($"Formant {p.FormantSemitones:+0.#;-0.#} st");
        if (p.Breathiness > 0.02f) parts.Add("Breathy");
        if (p.Rasp > 0.02f) parts.Add("Rasp");
        if (p.TremorPitchDepth > 0.02f || p.TremorAmpDepth > 0.02f) parts.Add("Tremor");
        if (p.RobotMix > 0.02f) parts.Add("Robot");
        if (p.ReverbMix > 0.02f) parts.Add("Reverb");
        if (p.EchoMix > 0.02f) parts.Add("Echo");
        if (p.RadioMix > 0.02f) parts.Add("Radio");
        if (p.ChorusMix > 0.02f) parts.Add("Chorus");
        if (p.Crush > 0.02f) parts.Add("Lo-fi");
        return parts.Count == 0 ? "Clean" : string.Join(" · ", parts);
    }
}

public sealed record SavePresetResult(string Name, string Category, string Description, string Icon);

/// <summary>Abstraction over modal UI so the view model stays testable.</summary>
public interface IDialogService
{
    SavePresetResult? ShowSavePreset(string name, string category, string description, string icon, bool isNew);
    bool Confirm(string title, string message);
    string? OpenFile(string filter);
    string? SaveFile(string filter, string defaultName);
    void ShowSettings();
    void ShowMessage(string title, string message);
}
