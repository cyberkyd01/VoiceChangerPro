using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiceChanger.Core.Dsp;
using VoiceChanger.Core.Logging;
using VoiceChanger.Core.Presets;
using VoiceChanger.Mac.Audio;

namespace VoiceChanger.Mac.ViewModels;

public sealed class PresetItem
{
    public PresetItem(VoicePreset preset) => Preset = preset;
    public VoicePreset Preset { get; }
    public string Icon => Preset.Icon;
    public string Name => Preset.Name;
    public string Description => Preset.Description;
    public string Category => Preset.Category;
}

public sealed partial class MainViewModel : ObservableObject
{
    private const string AllCategories = "All voices";

    private readonly MacSettings _settings = MacSettings.Load();
    private readonly MacVoiceEngine _engine = new();
    private readonly DispatcherTimer _meterTimer;
    private readonly DispatcherTimer _deviceTimer;
    private readonly VoiceProfile _profile;
    private string _deviceFingerprint = CoreAudio.DeviceListFingerprint();
    private string? _originalDefaultInput;
    private bool _loading = true;
    private bool _restartPending;

    public ObservableCollection<MacAudioDevice> Microphones { get; } = new();
    public ObservableCollection<MacAudioDevice> Cables { get; } = new();
    public ObservableCollection<MacAudioDevice> MonitorOutputs { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();
    public ObservableCollection<PresetItem> Presets { get; } = new();

    [ObservableProperty] private MacAudioDevice? _selectedMicrophone;
    [ObservableProperty] private MacAudioDevice? _selectedCable;
    [ObservableProperty] private MacAudioDevice? _selectedMonitor;
    [ObservableProperty] private bool _monitorEnabled;
    [ObservableProperty] private double _monitorVolume;
    [ObservableProperty] private bool _masterEnabled;
    [ObservableProperty] private bool _autoSetDefaultMicrophone;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _statusText = "Stopped";
    [ObservableProperty] private string _cableStatus = "";
    [ObservableProperty] private bool _cableOk;
    [ObservableProperty] private bool _cableMissing;
    [ObservableProperty] private string? _warning;
    [ObservableProperty] private bool _showMicPermissionHelp;
    [ObservableProperty] private double _inputLevel;
    [ObservableProperty] private double _outputLevel;
    [ObservableProperty] private string _pitchText = "—";
    [ObservableProperty] private string _latencyText = "";
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _selectedCategory = AllCategories;
    [ObservableProperty] private PresetItem? _selectedPreset;
    [ObservableProperty] private string _activePresetName = "";
    [ObservableProperty] private string _activePresetDescription = "";

    // Voice character (working profile) and clean-up
    [ObservableProperty] private double _pitch;
    [ObservableProperty] private double _formant;
    [ObservableProperty] private double _breathiness;
    [ObservableProperty] private double _rasp;
    [ObservableProperty] private double _reverb;
    [ObservableProperty] private double _outputGain;
    [ObservableProperty] private double _noiseReduction;
    [ObservableProperty] private double _voiceIsolation;
    [ObservableProperty] private double _voiceVolume;

    public string VersionText { get; } = $"v{typeof(MainViewModel).Assembly.GetName().Version?.ToString(3)}";

    public MainViewModel()
    {
        _profile = _settings.Profile;
        _monitorEnabled = _settings.MonitorEnabled;
        _monitorVolume = _settings.MonitorVolume;
        _masterEnabled = _settings.MasterEnabled;
        _autoSetDefaultMicrophone = _settings.AutoSetDefaultMicrophone;
        _noiseReduction = _settings.NoiseReduction;
        _voiceIsolation = _settings.VoiceIsolation;
        _voiceVolume = _settings.VoiceVolume;
        LoadProfileIntoSliders();

        Categories.Add(AllCategories);
        foreach (var c in PresetCategories.All.Where(c => c != PresetCategories.Custom && BuiltInPresets.All.Any(p => p.Category == c)))
            Categories.Add(c);
        FilterPresets();
        var active = BuiltInPresets.All.FirstOrDefault(p => p.Id == _settings.PresetId);
        ActivePresetName = active?.Name ?? "Custom voice";
        ActivePresetDescription = active?.Description ?? "";
        SelectedPreset = Presets.FirstOrDefault(p => p.Preset.Id == _settings.PresetId);

        _engine.Faulted += msg => Dispatcher.UIThread.Post(() => OnEngineFaulted(msg));
        RefreshDevices();
        _loading = false;

        _meterTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Background, (_, _) => UpdateMeters());
        _meterTimer.Start();
        _deviceTimer = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, _) => CheckDevices());
        _deviceTimer.Start();

        if (_settings.StartOnLaunch && SelectedMicrophone != null)
            Dispatcher.UIThread.Post(() => Start(), DispatcherPriority.Background);
    }

    // ───────────────────────────── Devices ─────────────────────────────

    private void RefreshDevices()
    {
        bool wasLoading = _loading;
        _loading = true;
        try
        {
            var all = MacAudio.Devices();
            int defIn = MacAudio.DefaultInputIndex, defOut = MacAudio.DefaultOutputIndex;

            Microphones.Clear();
            foreach (var d in all.Where(d => d.HasInput && !d.IsVirtual)) Microphones.Add(d);
            Cables.Clear();
            foreach (var d in all.Where(d => d.HasOutput && d.IsCable).OrderBy(d => DeviceClassifier.CableRank(d.Name))) Cables.Add(d);
            MonitorOutputs.Clear();
            foreach (var d in all.Where(d => d.HasOutput && !d.IsVirtual)) MonitorOutputs.Add(d);

            SelectedMicrophone = Microphones.FirstOrDefault(d => d.Name == _settings.MicrophoneName)
                                 ?? Microphones.FirstOrDefault(d => d.Index == defIn)
                                 ?? Microphones.FirstOrDefault(d => d.Name.Contains("MacBook", StringComparison.OrdinalIgnoreCase) || d.Name.Contains("Built-in", StringComparison.OrdinalIgnoreCase))
                                 ?? Microphones.FirstOrDefault();
            SelectedCable = Cables.FirstOrDefault(d => d.Name == _settings.CableName) ?? Cables.FirstOrDefault();
            SelectedMonitor = MonitorOutputs.FirstOrDefault(d => d.Name == _settings.MonitorName)
                              ?? MonitorOutputs.FirstOrDefault(d => d.Index == defOut)
                              ?? MonitorOutputs.FirstOrDefault();
            UpdateCableStatus();
        }
        catch (Exception ex)
        {
            Log.Error("UI", "Device refresh failed", ex);
            Warning = "Could not read the audio devices: " + ex.Message;
        }
        finally { _loading = wasLoading; }
    }

    private void CheckDevices()
    {
        var fp = CoreAudio.DeviceListFingerprint();
        if (fp == _deviceFingerprint) return;
        _deviceFingerprint = fp;
        Log.Info("UI", "Audio devices changed");
        bool wasRunning = IsRunning;
        if (wasRunning) StopEngine(restoreDefault: false);
        MacAudio.Restart();
        RefreshDevices();
        if (wasRunning) Start();
    }

    [RelayCommand]
    private void Refresh()
    {
        bool wasRunning = IsRunning;
        if (wasRunning) StopEngine(restoreDefault: false);
        MacAudio.Restart();
        _deviceFingerprint = CoreAudio.DeviceListFingerprint();
        RefreshDevices();
        if (wasRunning) Start();
    }

    private void UpdateCableStatus()
    {
        CableMissing = Cables.Count == 0;
        if (SelectedCable == null)
        {
            CableOk = false;
            CableStatus = "No virtual microphone installed. Install BlackHole 2ch (free) so Zoom, Discord, FaceTime and games can hear your changed voice.";
            return;
        }
        var current = CoreAudio.DefaultInputName;
        CableOk = current == SelectedCable.Name;
        CableStatus = CableOk
            ? $"'{SelectedCable.Name}' is your Mac's microphone. Every app now hears your changed voice."
            : $"In your apps, choose '{SelectedCable.Name}' as the microphone, or click 'Use in all apps'.";
    }

    partial void OnSelectedMicrophoneChanged(MacAudioDevice? value)
    {
        if (_loading || value == null) return;
        _settings.MicrophoneName = value.Name;
        _settings.Save();
        if (IsRunning) Start();
    }

    partial void OnSelectedCableChanged(MacAudioDevice? value)
    {
        if (_loading) return;
        _settings.CableName = value?.Name;
        _settings.Save();
        _engine.SetCable(value);
        if (IsRunning && value != null && AutoSetDefaultMicrophone) MakeCableDefault();
        UpdateCableStatus();
    }

    partial void OnSelectedMonitorChanged(MacAudioDevice? value)
    {
        if (_loading) return;
        _settings.MonitorName = value?.Name;
        _settings.Save();
        if (MonitorEnabled) _engine.SetMonitor(value, (float)MonitorVolume);
    }

    partial void OnMonitorEnabledChanged(bool value)
    {
        _settings.MonitorEnabled = value;
        _settings.Save();
        _engine.SetMonitor(value ? SelectedMonitor : null, (float)MonitorVolume);
    }

    partial void OnMonitorVolumeChanged(double value)
    {
        _settings.MonitorVolume = (float)value;
        _engine.SetMonitorVolume((float)value);
        SaveSoon();
    }

    partial void OnAutoSetDefaultMicrophoneChanged(bool value)
    {
        _settings.AutoSetDefaultMicrophone = value;
        _settings.Save();
        if (value && IsRunning) MakeCableDefault();
    }

    partial void OnMasterEnabledChanged(bool value)
    {
        _settings.MasterEnabled = value;
        _settings.Save();
        if (_engine.Pipeline != null) _engine.Pipeline.Bypass = !value;
    }

    // ───────────────────────────── Start / stop ─────────────────────────────

    [RelayCommand]
    private void ToggleRunning()
    {
        if (IsRunning) { StopEngine(restoreDefault: true); _settings.StartOnLaunch = false; }
        else { Start(); _settings.StartOnLaunch = IsRunning; }
        _settings.Save();
    }

    private void Start()
    {
        Warning = null;
        ShowMicPermissionHelp = false;
        if (SelectedMicrophone == null) { Warning = "No microphone found. Connect one and click Refresh."; return; }
        var monitor = MonitorEnabled ? SelectedMonitor : null;
        bool ok = _engine.Start(SelectedMicrophone, SelectedCable, monitor, (float)MonitorVolume, _profile, CurrentGlobal, bypass: !MasterEnabled);
        IsRunning = ok;
        _engine.VoiceVolume = (float)VoiceVolume;
        if (!ok) { Warning = "Could not start the microphone: " + _engine.LastError; StatusText = "Stopped"; return; }
        if (_engine.LastError != null) Warning = _engine.LastError;
        StatusText = "Running";
        LatencyText = _engine.Pipeline != null ? $"{_engine.Pipeline.LatencyMs + 2 * 1000.0 * MacVoiceEngine.FramesPerBuffer / _engine.SampleRate + 10:0} ms" : "";
        if (AutoSetDefaultMicrophone && SelectedCable != null) MakeCableDefault();
        UpdateCableStatus();
    }

    private void StopEngine(bool restoreDefault)
    {
        _engine.Stop();
        IsRunning = false;
        StatusText = "Stopped";
        InputLevel = OutputLevel = 0;
        PitchText = "—";
        if (restoreDefault) RestoreDefaultMicrophone();
        UpdateCableStatus();
    }

    private void OnEngineFaulted(string message)
    {
        if (!IsRunning) return;
        Log.Warn("UI", message);
        Warning = message + ". Reconnecting…";
        _restartPending = true;
    }

    // ───────────────────────────── Default microphone ─────────────────────────────

    [RelayCommand]
    private void MakeCableDefault()
    {
        if (SelectedCable == null) return;
        var current = CoreAudio.DefaultInputName;
        if (current == SelectedCable.Name) { UpdateCableStatus(); return; }
        _originalDefaultInput ??= current;
        if (!CoreAudio.SetDefaultInput(SelectedCable.Name))
            Warning = $"macOS did not accept '{SelectedCable.Name}' as the microphone. Choose it in System Settings › Sound › Input.";
        UpdateCableStatus();
    }

    [RelayCommand]
    private void RestoreDefaultMicrophone()
    {
        var target = _originalDefaultInput ?? SelectedMicrophone?.Name;
        if (target != null && CoreAudio.DefaultInputName == SelectedCable?.Name) CoreAudio.SetDefaultInput(target);
        _originalDefaultInput = null;
        UpdateCableStatus();
    }

    // ───────────────────────────── Presets ─────────────────────────────

    partial void OnSearchTextChanged(string value) => FilterPresets();
    partial void OnSelectedCategoryChanged(string value) => FilterPresets();

    private void FilterPresets()
    {
        var q = (SearchText ?? "").Trim().ToLowerInvariant();
        var keep = SelectedPreset;
        Presets.Clear();
        foreach (var p in BuiltInPresets.All)
        {
            if (SelectedCategory != AllCategories && p.Category != SelectedCategory) continue;
            if (q.Length > 0 && !p.SearchText.Contains(q)) continue;
            Presets.Add(new PresetItem(p));
        }
        if (keep != null) SelectedPreset = Presets.FirstOrDefault(p => p.Preset.Id == keep.Preset.Id);
    }

    partial void OnSelectedPresetChanged(PresetItem? value)
    {
        if (_loading || value == null || value.Preset.Id == _settings.PresetId) return;
        ApplyPreset(value.Preset);
    }

    private void ApplyPreset(VoicePreset preset)
    {
        _profile.CopyFrom(preset.Profile, notify: false);
        _settings.PresetId = preset.Id;
        ActivePresetName = preset.Name;
        ActivePresetDescription = preset.Description;
        LoadProfileIntoSliders();
        PushProfile();
        Log.Info("UI", $"Applied preset '{preset.Name}'");
    }

    [RelayCommand]
    private void ResetToPreset()
    {
        var preset = BuiltInPresets.All.FirstOrDefault(p => p.Id == _settings.PresetId);
        if (preset == null) return;
        _settings.PresetId = "";
        ApplyPreset(preset);
    }

    private void LoadProfileIntoSliders()
    {
        bool wasLoading = _loading;
        _loading = true;
        Pitch = _profile.PitchSemitones;
        Formant = _profile.FormantSemitones;
        Breathiness = _profile.Breathiness;
        Rasp = _profile.Rasp;
        Reverb = _profile.ReverbMix;
        OutputGain = _profile.OutputGainDb;
        _loading = wasLoading;
    }

    partial void OnPitchChanged(double value) => EditProfile(p => p.PitchSemitones = (float)value);
    partial void OnFormantChanged(double value) => EditProfile(p => p.FormantSemitones = (float)value);
    partial void OnBreathinessChanged(double value) => EditProfile(p => p.Breathiness = (float)value);
    partial void OnRaspChanged(double value) => EditProfile(p => p.Rasp = (float)value);
    partial void OnReverbChanged(double value) => EditProfile(p => p.ReverbMix = (float)value);
    partial void OnOutputGainChanged(double value) => EditProfile(p => p.OutputGainDb = (float)value);

    private void EditProfile(Action<VoiceProfile> edit)
    {
        if (_loading) return;
        edit(_profile);
        PushProfile();
    }

    private void PushProfile()
    {
        _engine.Pipeline?.Apply(_profile);
        _settings.Profile = _profile;
        SaveSoon();
    }

    private GlobalProcessing CurrentGlobal => new((float)NoiseReduction, (float)VoiceIsolation);

    partial void OnNoiseReductionChanged(double value) { _settings.NoiseReduction = (float)value; _engine.Pipeline?.ApplyGlobal(CurrentGlobal); SaveSoon(); }
    partial void OnVoiceIsolationChanged(double value) { _settings.VoiceIsolation = (float)value; _engine.Pipeline?.ApplyGlobal(CurrentGlobal); SaveSoon(); }
    partial void OnVoiceVolumeChanged(double value) { _settings.VoiceVolume = (float)value; _engine.VoiceVolume = (float)value; SaveSoon(); }

    // ───────────────────────────── Meters & housekeeping ─────────────────────────────

    private DateTime _saveAt = DateTime.MaxValue;
    private void SaveSoon() { if (!_loading) _saveAt = DateTime.UtcNow.AddMilliseconds(600); }

    private static double ToMeter(float peak) => peak <= 1e-6f ? 0 : Math.Clamp((20 * Math.Log10(peak) + 60) / 60, 0, 1);

    private void UpdateMeters()
    {
        if (DateTime.UtcNow >= _saveAt) { _saveAt = DateTime.MaxValue; _settings.Save(); }
        if (_restartPending)
        {
            _restartPending = false;
            Refresh();
            return;
        }
        var p = _engine.Pipeline;
        if (!IsRunning || p == null) return;
        InputLevel = ToMeter(p.InputPeak);
        OutputLevel = ToMeter(p.OutputPeak);
        var hz = p.DetectedPitchHz;
        PitchText = hz > 40 ? $"{hz:0} Hz" : "—";
        bool silent = _engine.SilentSeconds > 3;
        if (silent != ShowMicPermissionHelp) ShowMicPermissionHelp = silent;
    }

    [RelayCommand]
    private void OpenMicrophonePrivacy() => OpenUrl("x-apple.systempreferences:com.apple.preference.security?Privacy_Microphone");

    [RelayCommand]
    private void OpenSoundSettings() => OpenUrl("x-apple.systempreferences:com.apple.preference.sound");

    [RelayCommand]
    private void OpenBlackHole() => OpenUrl(DeviceClassifier.BlackHoleUrl);

    [RelayCommand]
    private void OpenLogs() => OpenUrl(Program.LogDirectory);

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo("open", $"\"{url}\"") { UseShellExecute = false }); }
        catch (Exception ex) { Log.Error("UI", $"Could not open {url}", ex); }
    }

    public void Shutdown()
    {
        _meterTimer.Stop();
        _deviceTimer.Stop();
        _settings.StartOnLaunch = IsRunning || _settings.StartOnLaunch;
        _engine.Stop();
        RestoreDefaultMicrophone();
        _settings.Save();
    }
}
