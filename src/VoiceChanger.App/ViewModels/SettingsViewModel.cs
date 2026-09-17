using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using VoiceChanger.App.Services;
using VoiceChanger.Core.Audio.Engine;
using VoiceChanger.Core.Presets;
using VoiceChanger.Core.Settings;

namespace VoiceChanger.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsStore _store;
    private readonly int _initialBuffer;

    public AppSettings Settings => _store.Settings;
    public ObservableCollection<int> BufferOptions { get; } = new() { 10, 15, 20, 30, 50 };
    public ObservableCollection<VoicePreset> DefaultPresetOptions { get; } = new();

    [ObservableProperty] private int _bufferMilliseconds;
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _startMinimized;
    [ObservableProperty] private bool _minimizeToTray;
    [ObservableProperty] private bool _showNotifications;
    [ObservableProperty] private bool _autoSetDefaultMicrophone;
    [ObservableProperty] private bool _restoreDefaultOnExit;
    [ObservableProperty] private bool _autoActivateNewMicrophones;
    [ObservableProperty] private VoicePreset? _defaultPreset;

    public string Version => typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
    public string LogFolder => Core.Logging.Log.Directory;
    public string PresetFolder => Core.Logging.AppPaths.Presets;

    public SettingsViewModel(SettingsStore store, PresetStore presets, EngineManager manager)
    {
        _store = store;
        var s = store.Settings;
        _initialBuffer = s.BufferMilliseconds;
        _bufferMilliseconds = BufferOptions.Contains(s.BufferMilliseconds) ? s.BufferMilliseconds : 20;
        _startWithWindows = s.StartWithWindows;
        _startMinimized = s.StartMinimized;
        _minimizeToTray = s.MinimizeToTray;
        _showNotifications = s.ShowNotifications;
        _autoSetDefaultMicrophone = s.AutoSetDefaultMicrophone;
        _restoreDefaultOnExit = s.RestoreDefaultOnExit;
        _autoActivateNewMicrophones = s.AutoActivateNewMicrophones;
        foreach (var p in presets.All) DefaultPresetOptions.Add(p);
        _defaultPreset = DefaultPresetOptions.FirstOrDefault(p => p.Id == s.DefaultPresetId) ?? DefaultPresetOptions.FirstOrDefault();
    }

    /// <summary>Writes the values back and returns true when the audio buffer size changed (engines must restart).</summary>
    public bool Apply()
    {
        var s = Settings;
        s.BufferMilliseconds = BufferMilliseconds;
        s.StartWithWindows = StartWithWindows;
        s.StartMinimized = StartMinimized;
        s.MinimizeToTray = MinimizeToTray;
        s.ShowNotifications = ShowNotifications;
        s.AutoSetDefaultMicrophone = AutoSetDefaultMicrophone;
        s.RestoreDefaultOnExit = RestoreDefaultOnExit;
        s.AutoActivateNewMicrophones = AutoActivateNewMicrophones;
        if (DefaultPreset != null) s.DefaultPresetId = DefaultPreset.Id;
        StartupService.Sync(StartWithWindows);
        _store.SaveSoon();
        return BufferMilliseconds != _initialBuffer;
    }
}
