using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiceChanger.Core.Audio.Devices;
using VoiceChanger.Core.Audio.Engine;
using VoiceChanger.Core.Logging;
using VoiceChanger.Core.Presets;
using VoiceChanger.Core.Settings;

namespace VoiceChanger.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly EngineManager _manager;
    private readonly PresetStore _presets;
    private readonly SettingsStore _settingsStore;
    private readonly DeviceManager _devices;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _meterTimer;
    private readonly DispatcherTimer _notificationTimer;
    private bool _loadingDevices;
    private VoiceProfile? _observedProfile;

    public IDialogService Dialogs { get; set; } = null!;

    public const string AllCategories = "All";
    public const string FavoritesCategory = "★ Favorites";

    public ObservableCollection<MicrophoneItemViewModel> Microphones { get; } = new();
    public ObservableCollection<PresetItemViewModel> Presets { get; } = new();
    public ICollectionView PresetsView { get; }
    public ObservableCollection<string> Categories { get; } = new();
    public ObservableCollection<AudioDeviceInfo> RenderDevices { get; } = new();
    public ObservableCollection<AudioDeviceInfo> CaptureDevices { get; } = new();
    public ObservableCollection<AudioDeviceInfo> MonitorDevices { get; } = new();

    [ObservableProperty] private MicrophoneItemViewModel? _selectedMicrophone;
    [ObservableProperty] private bool _masterEnabled;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _selectedCategory = AllCategories;
    [ObservableProperty] private string _cableStatus = "";
    [ObservableProperty] private string _cableDetail = "";
    [ObservableProperty] private bool _cableOk;
    [ObservableProperty] private bool _defaultMicIsCable;
    [ObservableProperty] private AudioDeviceInfo? _selectedCableRender;
    [ObservableProperty] private AudioDeviceInfo? _selectedCableCapture;
    [ObservableProperty] private AudioDeviceInfo? _selectedMonitorDevice;
    [ObservableProperty] private string _currentPresetName = "";
    [ObservableProperty] private bool _isModified;
    [ObservableProperty] private bool _canOverwritePreset;

    // meters
    [ObservableProperty] private double _inputLevel;
    [ObservableProperty] private double _outputLevel;
    [ObservableProperty] private string _inputLevelText = "-inf dB";
    [ObservableProperty] private string _outputLevelText = "-inf dB";
    [ObservableProperty] private string _gainReductionText = "0.0 dB";
    [ObservableProperty] private bool _gateOpen;
    [ObservableProperty] private string _pitchText = "—";
    [ObservableProperty] private string _latencyText = "";
    [ObservableProperty] private string _statusText = "";

    // notifications (InfoBar)
    [ObservableProperty] private bool _notificationOpen;
    [ObservableProperty] private string _notificationTitle = "";
    [ObservableProperty] private string _notificationMessage = "";
    [ObservableProperty] private bool _notificationIsWarning;

    // global (preset-independent) clean-up
    [ObservableProperty] private double _noiseReduction;
    [ObservableProperty] private double _voiceIsolation;
    [ObservableProperty] private string _noiseFloorText = "";
    [ObservableProperty] private string _isolationText = "";

    partial void OnNoiseReductionChanged(double value) => _manager.SetGlobalProcessing((float)value, (float)VoiceIsolation);
    partial void OnVoiceIsolationChanged(double value) => _manager.SetGlobalProcessing((float)NoiseReduction, (float)value);

    [RelayCommand]
    private void ResetGlobalCleanup()
    {
        NoiseReduction = 0;
        VoiceIsolation = 0;
    }

    // ───────────────────────────── Background audio bed ─────────────────────────────
    [ObservableProperty] private string _backgroundFileName = "No file selected";
    [ObservableProperty] private bool _backgroundHasFile;
    [ObservableProperty] private bool _backgroundPlaying;
    [ObservableProperty] private bool _backgroundLoop = true;
    [ObservableProperty] private double _backgroundVolume = 0.5;
    [ObservableProperty] private string _backgroundStatus = "";
    [ObservableProperty] private double _backgroundPosition;
    [ObservableProperty] private double _backgroundDuration = 1;
    private bool _loadingBackground;

    partial void OnBackgroundPlayingChanged(bool value) { if (!_loadingBackground) _manager.SetBackgroundPlaying(value); }
    partial void OnBackgroundLoopChanged(bool value) { if (!_loadingBackground) _manager.SetBackgroundLoop(value); }
    partial void OnBackgroundVolumeChanged(double value) { if (!_loadingBackground) _manager.SetBackgroundVolume((float)value); }

    [ObservableProperty] private double _voiceVolume = 1.0;
    partial void OnVoiceVolumeChanged(double value) { if (!_loadingBackground) _manager.SetVoiceVolume((float)value); }

    private void RefreshBackground()
    {
        _loadingBackground = true;
        try
        {
            var path = Settings.BackgroundFilePath;
            BackgroundHasFile = !string.IsNullOrEmpty(path);
            BackgroundFileName = BackgroundHasFile ? System.IO.Path.GetFileName(path!) : "No file selected";
            BackgroundPlaying = Settings.BackgroundPlaying;
            BackgroundLoop = Settings.BackgroundLoop;
            BackgroundVolume = Settings.BackgroundVolume;
            VoiceVolume = Settings.VoiceVolume;
        }
        finally { _loadingBackground = false; }
        UpdateBackgroundStatus();
    }

    private void UpdateBackgroundStatus()
    {
        var t = _manager.Background;
        if (!BackgroundHasFile) { BackgroundStatus = "Pick a music or ambience file to play under your voice."; BackgroundPosition = 0; return; }
        if (t == null)
        {
            BackgroundStatus = _manager.Engines.Any(e => e.State == EngineState.Running) ? "Ready" : "Waiting for an active microphone";
            return;
        }
        if (t.Error != null) { BackgroundStatus = "Cannot play: " + t.Error; return; }
        if (!t.IsReady) { BackgroundStatus = "Loading…"; return; }
        BackgroundDuration = Math.Max(1, t.DurationSeconds);
        BackgroundPosition = Math.Min(t.PositionSeconds, BackgroundDuration);
        string pos = $"{TimeSpan.FromSeconds(t.PositionSeconds):m\\:ss} / {TimeSpan.FromSeconds(t.DurationSeconds):m\\:ss}";
        string on = _manager.BackgroundEngine != null ? $" · on {_manager.BackgroundEngine.DeviceName}" : "";
        BackgroundStatus = (t.Ended ? "Finished" : t.Playing ? "Playing" : "Paused") + " " + pos + on;
    }

    [RelayCommand]
    private void ChooseBackgroundFile()
    {
        var path = Dialogs.OpenFile("Audio files|*.mp3;*.wav;*.m4a;*.aac;*.wma;*.flac;*.mp4;*.aiff;*.aif;*.ogg;*.opus|All files|*.*");
        if (path == null) return;
        var err = _manager.LoadBackgroundFile(path);
        RefreshBackground();
        if (err != null) ShowNotification("Background audio", err, true);
        else ShowNotification("Background audio loaded", System.IO.Path.GetFileName(path) + " — press Play to start it under your voice");
    }

    [RelayCommand]
    private void ClearBackgroundFile()
    {
        _manager.LoadBackgroundFile(null);
        _manager.SetBackgroundPlaying(false);
        RefreshBackground();
    }

    [RelayCommand]
    private void RestartBackground() => _manager.SeekBackground(0);

    public string VbCableUrl => VirtualCableDetector.VbCableDownloadUrl;
    public AppSettings Settings => _settingsStore.Settings;

    public MainViewModel(EngineManager manager, PresetStore presets, SettingsStore settingsStore, DeviceManager devices, Dispatcher dispatcher)
    {
        _manager = manager;
        _presets = presets;
        _settingsStore = settingsStore;
        _devices = devices;
        _dispatcher = dispatcher;

        PresetsView = CollectionViewSource.GetDefaultView(Presets);
        PresetsView.Filter = FilterPreset;
        PresetsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PresetItemViewModel.Category)));
        PresetsView.SortDescriptions.Add(new SortDescription(nameof(PresetItemViewModel.Category), ListSortDirection.Ascending));
        PresetsView.SortDescriptions.Add(new SortDescription(nameof(PresetItemViewModel.Name), ListSortDirection.Ascending));

        Categories.Add(AllCategories);
        Categories.Add(FavoritesCategory);
        foreach (var c in PresetCategories.All) Categories.Add(c);

        _masterEnabled = manager.MasterEnabled;
        _noiseReduction = Settings.NoiseReduction;
        _voiceIsolation = Settings.VoiceIsolation;

        _manager.EnginesChanged += () => _dispatcher.BeginInvoke(RefreshEngines);
        _manager.VirtualCableChanged += () => _dispatcher.BeginInvoke(RefreshCable);
        _manager.Notification += (_, e) => _dispatcher.BeginInvoke(() =>
        {
            ShowNotification(e.Title, e.Message, e.IsWarning);
            App.Current.Tray?.ShowBalloon(e.Title, e.Message, e.IsWarning);
        });
        _presets.Changed += () => _dispatcher.BeginInvoke(LoadPresets);

        _meterTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(40) };
        _meterTimer.Tick += (_, _) => UpdateMeters();
        _meterTimer.Start();

        _notificationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _notificationTimer.Tick += (_, _) => { NotificationOpen = false; _notificationTimer.Stop(); };

        LoadPresets();
        RefreshDeviceLists();
        RefreshEngines();
        RefreshCable();
        RefreshBackground();
    }

    // ───────────────────────────── Engines / microphones ─────────────────────────────

    public void RefreshEngines()
    {
        var engines = _manager.Engines;
        var selectedId = SelectedMicrophone?.Id;

        foreach (var vm in Microphones.ToList())
            if (engines.All(e => e.DeviceId != vm.Id)) Microphones.Remove(vm);
        foreach (var e in engines)
        {
            var existing = Microphones.FirstOrDefault(m => m.Id == e.DeviceId);
            if (existing == null) Microphones.Add(new MicrophoneItemViewModel(_manager, e));
            else if (!ReferenceEquals(existing.Engine, e))
            {
                int idx = Microphones.IndexOf(existing);
                Microphones[idx] = new MicrophoneItemViewModel(_manager, e);
            }
            else existing.Refresh();
        }

        if (SelectedMicrophone == null || Microphones.All(m => m.Id != selectedId))
            SelectedMicrophone = Microphones.FirstOrDefault(m => m.IsRunning) ?? Microphones.FirstOrDefault();
        else
            SelectedMicrophone = Microphones.First(m => m.Id == selectedId);

        MasterEnabled = _manager.MasterEnabled;
        UpdateStatus();
        RefreshCable();
    }

    public void RefreshMaster() => MasterEnabled = _manager.MasterEnabled;

    partial void OnMasterEnabledChanged(bool value)
    {
        if (_manager.MasterEnabled != value) _manager.MasterEnabled = value;
        foreach (var m in Microphones) m.Refresh();
        UpdateStatus();
    }

    partial void OnSelectedMicrophoneChanged(MicrophoneItemViewModel? value)
    {
        if (_observedProfile != null) _observedProfile.PropertyChanged -= OnProfileChanged;
        _observedProfile = value?.Profile;
        if (_observedProfile != null) _observedProfile.PropertyChanged += OnProfileChanged;
        UpdatePresetState();
        UpdateStatus();
    }

    private void OnProfileChanged(object? sender, PropertyChangedEventArgs e) => UpdatePresetState();

    private void UpdatePresetState()
    {
        var mic = SelectedMicrophone;
        if (mic == null)
        {
            CurrentPresetName = "";
            IsModified = false;
            CanOverwritePreset = false;
            foreach (var p in Presets) p.IsApplied = false;
            return;
        }
        var preset = _presets.Find(mic.Engine.Config.PresetId);
        CurrentPresetName = preset?.Name ?? mic.Engine.Config.PresetName ?? "Custom";
        IsModified = preset == null ? !mic.Profile.IsNeutral : !mic.Profile.ValueEquals(preset.Profile);
        CanOverwritePreset = preset is { IsBuiltIn: false };
        foreach (var p in Presets) p.IsApplied = preset != null && p.Id == preset.Id;
        mic.PresetName = CurrentPresetName;
    }

    private void UpdateStatus()
    {
        int running = Microphones.Count(m => m.IsRunning);
        StatusText = Microphones.Count == 0 ? "No microphones detected"
            : $"{running} of {Microphones.Count} microphone(s) live" + (MasterEnabled ? "" : " · effects OFF (pass-through)");
    }

    private void UpdateMeters()
    {
        UpdateBackgroundStatus();
        var mic = SelectedMicrophone;
        var pipe = mic?.Engine.Pipeline;
        if (pipe == null || mic == null || !mic.IsRunning)
        {
            InputLevel = 0; OutputLevel = 0;
            InputLevelText = "-inf dB"; OutputLevelText = "-inf dB"; GainReductionText = "0.0 dB";
            GateOpen = false; PitchText = "—"; LatencyText = "";
            return;
        }
        InputLevel = pipe.InputPeak;
        OutputLevel = pipe.OutputPeak;
        InputLevelText = FormatDb(pipe.InputPeak);
        OutputLevelText = FormatDb(pipe.OutputPeak);
        float gr = Math.Min(pipe.CompressorGainReductionDb, 0) + Math.Min(pipe.LimiterGainReductionDb, 0);
        GainReductionText = $"{gr:F1} dB";
        GateOpen = pipe.GateOpen;
        PitchText = pipe.DetectedPitchHz > 0 ? $"{pipe.DetectedPitchHz:F0} Hz" : "—";
        LatencyText = $"≈ {mic.Engine.EstimatedLatencyMs:F0} ms";
        NoiseFloorText = NoiseReduction > 0.005 ? $"noise floor {pipe.NoiseFloorDb:F0} dB" : "off";
        IsolationText = VoiceIsolation > 0.005 ? $"{pipe.IsolationGainReductionDb:F0} dB" : "off";
        CheckSilentInput(mic);
    }

    private string? _silenceWarnedFor;

    /// <summary>Warns once per microphone when a live microphone delivers only digital silence for several seconds.</summary>
    private void CheckSilentInput(MicrophoneItemViewModel mic)
    {
        var e = mic.Engine;
        if (e.EverHadSignal)
        {
            // real audio is arriving: any earlier "silent" or driver warning no longer applies
            _silenceWarnedFor = null;
            if (HasInputProblem) { HasInputProblem = false; InputProblemText = ""; }
            return;
        }
        if (e.InputSilentSeconds < 5 || mic.LoopClip) return;
        if (_silenceWarnedFor == mic.Id) return;
        _silenceWarnedFor = mic.Id;
        var reports = _manager.Interference;
        if (reports.Count > 0)
        {
            var r = reports[0];
            InputProblemText = $"No signal from '{mic.Name}'. {r.Product} ('{r.Component}') is intercepting every microphone on this PC and passing silence.";
            ShowNotification("Microphone is silent — another voice changer is blocking it", InputProblemText + " Click 'How to fix' in the status bar.", true);
            Log.Warn("Manager", $"Silent input on '{mic.Name}'; interference: {r.Product} [{r.Component}]");
        }
        else
        {
            InputProblemText = $"No signal from '{mic.Name}'. Check Windows Settings › Privacy › Microphone, the headset's mute switch and the input level.";
            ShowNotification("Microphone is silent", InputProblemText, true);
            Log.Warn("Manager", $"Silent input on '{mic.Name}'; no known interference found");
        }
        HasInputProblem = true;
    }

    [ObservableProperty] private bool _hasInputProblem;
    [ObservableProperty] private string _inputProblemText = "";

    [RelayCommand]
    private void ShowInterferenceHelp()
    {
        var reports = _manager.Interference;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(InputProblemText);
        sb.AppendLine();
        if (reports.Count == 0)
        {
            sb.AppendLine("Checklist:");
            sb.AppendLine("• Settings › Privacy & security › Microphone: 'Microphone access' and 'Let desktop apps access your microphone' must be On.");
            sb.AppendLine("• Settings › System › Sound › Input: select the microphone, press 'Test your microphone' and check the level.");
            sb.AppendLine("• Headsets often have an inline or on-ear mute button.");
        }
        else
        {
            sb.AppendLine("Detected interfering drivers:");
            foreach (var r in reports)
            {
                sb.AppendLine($"• {r.Product} — {r.Detail}");
                sb.AppendLine($"  Fix: {r.Remedy}");
            }
            sb.AppendLine();
            sb.AppendLine("Only one voice changer can own the microphone path. After removing the other product, restart Windows and press Refresh here.");
        }
        Dialogs.ShowMessage("Why is my microphone silent?", sb.ToString());
    }

    [RelayCommand]
    private void OpenAppsSettings()
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:appsfeatures") { UseShellExecute = true }); } catch { }
    }

    private static string FormatDb(float lin) => lin <= 1e-5f ? "-inf dB" : $"{20 * Math.Log10(lin):F1} dB";

    [RelayCommand]
    private void RefreshDevices()
    {
        _manager.Refresh("manual");
        RefreshDeviceLists();
        RefreshEngines();
        ShowNotification("Devices refreshed", $"{_manager.CaptureDevices.Count} capture, {_manager.RenderDevices.Count} playback endpoints");
    }

    [RelayCommand]
    private void StartMicrophone(MicrophoneItemViewModel? mic) { if (mic != null) mic.Active = true; }

    [RelayCommand]
    private void StopMicrophone(MicrophoneItemViewModel? mic) { if (mic != null) mic.Active = false; }

    [RelayCommand]
    private void RestartMicrophone(MicrophoneItemViewModel? mic) { if (mic != null) _manager.RestartEngine(mic.Engine); }

    [RelayCommand]
    private void ActivateAll() { foreach (var m in Microphones) if (!m.Active) m.Active = true; }

    [RelayCommand]
    private void CopyToAllMicrophones()
    {
        if (SelectedMicrophone == null) return;
        _manager.CopyProfileToAll(SelectedMicrophone.Engine);
        ShowNotification("Applied everywhere", $"Settings of '{SelectedMicrophone.Name}' copied to all microphones");
    }

    // ───────────────────────────── Test clip ─────────────────────────────

    [RelayCommand]
    private void RecordClip()
    {
        var mic = SelectedMicrophone;
        if (mic == null || !mic.IsRunning) { ShowNotification("Microphone not live", "Activate the microphone first", true); return; }
        mic.LoopClip = false;
        mic.Engine.RecordClip(5);
        mic.Refresh();
        ShowNotification("Recording", "Speak now — recording 5 seconds of raw microphone audio");
        mic.Engine.ClipRecorded += OnClipRecorded;
    }

    private void OnClipRecorded(VoiceEngine e)
    {
        e.ClipRecorded -= OnClipRecorded;
        _dispatcher.BeginInvoke(() =>
        {
            var mic = Microphones.FirstOrDefault(m => m.Id == e.DeviceId);
            mic?.Refresh();
            ShowNotification("Clip ready", "Turn on 'Loop test clip' to hear it through the current settings while you tweak");
        });
    }

    [RelayCommand]
    private void ClearClip()
    {
        SelectedMicrophone?.Engine.ClearClip();
        SelectedMicrophone?.Refresh();
    }

    // ───────────────────────────── Presets ─────────────────────────────

    private void LoadPresets()
    {
        var favs = Settings.FavoritePresetIds.ToHashSet();
        Presets.Clear();
        foreach (var p in _presets.All) Presets.Add(new PresetItemViewModel(p, favs.Contains(p.Id)));
        UpdatePresetState();
        PresetsView.Refresh();
    }

    private bool FilterPreset(object o)
    {
        if (o is not PresetItemViewModel p) return false;
        if (SelectedCategory == FavoritesCategory && !p.IsFavorite) return false;
        if (SelectedCategory != AllCategories && SelectedCategory != FavoritesCategory && p.Category != SelectedCategory) return false;
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var q = SearchText.Trim().ToLowerInvariant();
            if (!p.Preset.SearchText.Contains(q)) return false;
        }
        return true;
    }

    partial void OnSearchTextChanged(string value) => PresetsView.Refresh();
    partial void OnSelectedCategoryChanged(string value) => PresetsView.Refresh();

    [RelayCommand]
    private void ApplyPreset(PresetItemViewModel? preset)
    {
        if (preset == null) return;
        var mic = SelectedMicrophone;
        if (mic == null) { ShowNotification("No microphone", "Connect a microphone to apply presets", true); return; }
        _manager.ApplyPreset(mic.Engine, preset.Preset);
        UpdatePresetState();
        mic.Refresh();
    }

    [RelayCommand]
    private void ApplyPresetToAll(PresetItemViewModel? preset)
    {
        if (preset == null) return;
        _manager.ApplyPresetToAll(preset.Preset);
        UpdatePresetState();
    }

    [RelayCommand]
    private void ToggleFavorite(PresetItemViewModel? preset)
    {
        if (preset == null) return;
        preset.IsFavorite = !preset.IsFavorite;
        if (preset.IsFavorite) { if (!Settings.FavoritePresetIds.Contains(preset.Id)) Settings.FavoritePresetIds.Add(preset.Id); }
        else Settings.FavoritePresetIds.Remove(preset.Id);
        _settingsStore.SaveSoon();
        if (SelectedCategory == FavoritesCategory) PresetsView.Refresh();
    }

    [RelayCommand]
    private void SavePreset()
    {
        var mic = SelectedMicrophone;
        if (mic == null) return;
        var current = _presets.Find(mic.Engine.Config.PresetId);
        if (current is { IsBuiltIn: false })
        {
            current.Profile = mic.Profile.Clone();
            _presets.Save(current);
            UpdatePresetState();
            ShowNotification("Preset saved", $"'{current.Name}' updated");
        }
        else SavePresetAs();
    }

    [RelayCommand]
    private void SavePresetAs()
    {
        var mic = SelectedMicrophone;
        if (mic == null) return;
        var current = _presets.Find(mic.Engine.Config.PresetId);
        var result = Dialogs.ShowSavePreset(
            current != null ? current.Name + (current.IsBuiltIn ? " (custom)" : "") : "My Voice",
            current is { IsBuiltIn: false } ? current.Category : PresetCategories.Custom,
            current?.Description ?? "",
            current?.Icon ?? "🎙",
            isNew: true);
        if (result == null) return;
        var preset = new VoicePreset
        {
            Name = result.Name,
            Category = result.Category,
            Description = result.Description,
            Icon = string.IsNullOrWhiteSpace(result.Icon) ? "🎙" : result.Icon,
            Tags = new[] { "custom" },
            Profile = mic.Profile.Clone()
        };
        var saved = _presets.Save(preset);
        mic.Engine.Config.PresetId = saved.Id;
        mic.Engine.Config.PresetName = saved.Name;
        _settingsStore.SaveSoon();
        LoadPresets();
        ShowNotification("Preset saved", $"'{saved.Name}' added to My Presets");
    }

    [RelayCommand]
    private void RenamePreset(PresetItemViewModel? preset)
    {
        if (preset == null || preset.IsBuiltIn) return;
        var r = Dialogs.ShowSavePreset(preset.Name, preset.Category, preset.Description, preset.Icon, isNew: false);
        if (r == null) return;
        var p = preset.Preset;
        p.Name = r.Name; p.Category = r.Category; p.Description = r.Description; p.Icon = r.Icon;
        _presets.Save(p);
        LoadPresets();
    }

    [RelayCommand]
    private void DeletePreset(PresetItemViewModel? preset)
    {
        if (preset == null || preset.IsBuiltIn) return;
        if (!Dialogs.Confirm("Delete preset", $"Delete '{preset.Name}' permanently?")) return;
        _presets.Delete(preset.Id);
        foreach (var m in Microphones)
            if (m.Engine.Config.PresetId == preset.Id) { m.Engine.Config.PresetId = null; m.Engine.Config.PresetName = "Custom"; }
        LoadPresets();
    }

    [RelayCommand]
    private void DuplicatePreset(PresetItemViewModel? preset)
    {
        if (preset == null) return;
        var copy = preset.Preset.Clone();
        copy.Id = Guid.NewGuid().ToString("N");
        copy.Name = preset.Name + " (copy)";
        copy.IsBuiltIn = false;
        copy.Category = preset.IsBuiltIn ? PresetCategories.Custom : preset.Category;
        _presets.Save(copy);
        LoadPresets();
        ShowNotification("Preset duplicated", $"'{copy.Name}' added to My Presets — edit it freely");
    }

    [RelayCommand]
    private void ExportPreset(PresetItemViewModel? preset)
    {
        if (preset == null) return;
        var path = Dialogs.SaveFile("Voice preset (*.json)|*.json", SafeName(preset.Name) + ".json");
        if (path == null) return;
        try { _presets.Export(preset.Preset, path); ShowNotification("Exported", path); }
        catch (Exception ex) { ShowNotification("Export failed", ex.Message, true); }
    }

    [RelayCommand]
    private void ImportPresets()
    {
        var path = Dialogs.OpenFile("Voice preset (*.json)|*.json|All files|*.*");
        if (path == null) return;
        try
        {
            var imported = _presets.Import(path);
            LoadPresets();
            ShowNotification("Imported", $"{imported.Count} preset(s) imported");
        }
        catch (Exception ex) { ShowNotification("Import failed", ex.Message, true); }
    }

    [RelayCommand]
    private void RevertToPreset()
    {
        var mic = SelectedMicrophone;
        if (mic == null) return;
        var preset = _presets.Find(mic.Engine.Config.PresetId);
        if (preset != null) _manager.ApplyPreset(mic.Engine, preset);
        else mic.Profile.Reset();
        UpdatePresetState();
    }

    [RelayCommand]
    private void ResetToNeutral()
    {
        var mic = SelectedMicrophone;
        if (mic == null) return;
        mic.Profile.Reset();
        mic.Engine.Config.PresetId = null;
        mic.Engine.Config.PresetName = "Custom";
        _settingsStore.SaveSoon();
        UpdatePresetState();
        mic.Refresh();
    }

    private static string SafeName(string s)
    {
        foreach (var c in System.IO.Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }

    // ───────────────────────────── Routing / cable ─────────────────────────────

    private void RefreshDeviceLists()
    {
        _loadingDevices = true;
        try
        {
            RenderDevices.Clear();
            foreach (var d in _manager.RenderDevices) RenderDevices.Add(d);
            CaptureDevices.Clear();
            foreach (var d in _manager.CaptureDevices) CaptureDevices.Add(d);
            MonitorDevices.Clear();
            foreach (var d in _manager.RenderDevices.Where(d => !d.IsVirtual)) MonitorDevices.Add(d);

            var monitorId = Settings.MonitorDeviceId ?? _devices.GetDefaultDeviceId(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.Role.Multimedia);
            SelectedMonitorDevice = MonitorDevices.FirstOrDefault(d => d.Id == monitorId) ?? MonitorDevices.FirstOrDefault();
        }
        finally { _loadingDevices = false; }
    }

    public void RefreshCable()
    {
        RefreshDeviceLists();
        var cable = _manager.ActiveCable;
        _loadingDevices = true;
        try
        {
            SelectedCableRender = cable != null ? RenderDevices.FirstOrDefault(d => d.Id == cable.RenderInput.Id) : null;
            SelectedCableCapture = cable?.CaptureOutput != null ? CaptureDevices.FirstOrDefault(d => d.Id == cable.CaptureOutput.Id) : null;
        }
        finally { _loadingDevices = false; }

        if (cable == null)
        {
            CableOk = false;
            CableStatus = "No virtual cable";
            CableDetail = "Install VB-CABLE (free) so games, browsers and calling apps can use your changed voice.";
            DefaultMicIsCable = false;
            return;
        }
        var defaultMic = _devices.GetDefaultDeviceId(NAudio.CoreAudioApi.DataFlow.Capture, NAudio.CoreAudioApi.Role.Communications);
        DefaultMicIsCable = cable.CaptureOutput != null && defaultMic == cable.CaptureOutput.Id;
        CableOk = cable.CaptureOutput != null;
        CableStatus = CableOk ? $"{cable.Vendor} ready" : $"{cable.Vendor}: output found, microphone endpoint missing";
        CableDetail = CableOk
            ? (DefaultMicIsCable
                ? $"Windows default microphone is '{cable.CaptureOutput!.Name}'. Every app now hears your processed voice."
                : $"Select '{cable.CaptureOutput!.Name}' as microphone in your apps, or click 'Set as Windows default mic'.")
            : "Pick the matching capture endpoint in Settings › Routing.";
    }

    partial void OnSelectedCableRenderChanged(AudioDeviceInfo? value)
    {
        if (_loadingDevices) return;
        _manager.SelectVirtualCable(value?.Id, SelectedCableCapture?.Id);
        RefreshCable();
    }

    partial void OnSelectedCableCaptureChanged(AudioDeviceInfo? value)
    {
        if (_loadingDevices) return;
        _manager.SelectVirtualCable(SelectedCableRender?.Id, value?.Id);
        RefreshCable();
    }

    partial void OnSelectedMonitorDeviceChanged(AudioDeviceInfo? value)
    {
        if (_loadingDevices || value == null) return;
        _manager.SetMonitorDevice(value.Id);
    }

    [RelayCommand]
    private void SetDefaultMicrophone()
    {
        var cable = _manager.ActiveCable;
        if (cable?.CaptureOutput == null) { ShowNotification("No virtual cable", "Install or select a virtual cable first", true); return; }
        if (PolicyConfig.SetDefaultEndpoint(cable.CaptureOutput.Id))
            ShowNotification("Default microphone set", $"'{cable.CaptureOutput.Name}' is now the Windows default microphone");
        else
            ShowNotification("Could not set default", "Windows refused the change. Set it manually in Sound settings.", true);
        RefreshCable();
    }

    [RelayCommand]
    private void RestoreDefaultMicrophone()
    {
        _manager.RestoreDefaultMicrophone();
        var physical = _manager.CaptureDevices.FirstOrDefault(d => !d.IsVirtual);
        if (physical != null && !_manager.IsDefaultMicrophoneOverridden)
        {
            // manager had nothing to restore: fall back to the first physical microphone
            var current = _devices.GetDefaultDeviceId(NAudio.CoreAudioApi.DataFlow.Capture, NAudio.CoreAudioApi.Role.Communications);
            if (current != null && _manager.ActiveCable?.CaptureOutput?.Id == current)
                PolicyConfig.SetDefaultEndpoint(physical.Id);
        }
        RefreshCable();
        ShowNotification("Default microphone restored", "Apps will use your physical microphone directly");
    }

    [RelayCommand]
    private void OpenCableDownload()
    {
        try { Process.Start(new ProcessStartInfo(VbCableUrl) { UseShellExecute = true }); } catch { }
    }

    [RelayCommand]
    private void OpenSoundSettings()
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true }); } catch { }
    }

    [RelayCommand]
    private void OpenSettings() => Dialogs.ShowSettings();

    [RelayCommand]
    private void OpenLogs()
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", Log.Directory) { UseShellExecute = true }); } catch { }
    }

    // ───────────────────────────── Notifications ─────────────────────────────

    public void ShowNotification(string title, string message, bool isWarning = false)
    {
        NotificationTitle = title;
        NotificationMessage = message;
        NotificationIsWarning = isWarning;
        NotificationOpen = true;
        _notificationTimer.Stop();
        _notificationTimer.Start();
    }

    /// <summary>Called after settings dialog closes to apply changes that need engine restarts.</summary>
    public void OnSettingsChanged(bool bufferChanged)
    {
        if (bufferChanged) _manager.RestartAll();
        _manager.ApplyDefaultMicrophonePolicy();
        RefreshCable();
        RefreshEngines();
    }
}
