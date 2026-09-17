using NAudio.CoreAudioApi;
using VoiceChanger.Core.Audio.Devices;
using VoiceChanger.Core.Dsp;
using VoiceChanger.Core.Logging;
using VoiceChanger.Core.Presets;
using VoiceChanger.Core.Settings;

namespace VoiceChanger.Core.Audio.Engine;

public sealed class NotificationEventArgs : EventArgs
{
    public NotificationEventArgs(string title, string message, bool isWarning = false) { Title = title; Message = message; IsWarning = isWarning; }
    public string Title { get; }
    public string Message { get; }
    public bool IsWarning { get; }
}

/// <summary>
/// Owns one <see cref="VoiceEngine"/> per microphone, reacts to hot-plug events, manages the
/// virtual cable routing and the Windows default-microphone policy, and persists state.
/// All public members are safe to call from the UI thread; events may fire on background threads.
/// </summary>
public sealed class EngineManager : IDisposable
{
    private readonly DeviceManager _devices;
    private readonly SettingsStore _settings;
    private readonly PresetStore _presets;
    private readonly Dictionary<string, VoiceEngine> _engines = new();
    private readonly object _sync = new();
    private readonly Timer _refreshTimer;
    private string? _originalDefaultMic, _originalDefaultComMic;
    private bool _defaultOverridden;
    private bool _disposed;

    public event Action? EnginesChanged;
    public event EventHandler<NotificationEventArgs>? Notification;
    public event Action? VirtualCableChanged;

    public AppSettings Settings => _settings.Settings;
    public VirtualCable? ActiveCable { get; private set; }
    public IReadOnlyList<VirtualCable> DetectedCables { get; private set; } = Array.Empty<VirtualCable>();
    public IReadOnlyList<AudioDeviceInfo> CaptureDevices { get; private set; } = Array.Empty<AudioDeviceInfo>();
    public IReadOnlyList<AudioDeviceInfo> RenderDevices { get; private set; } = Array.Empty<AudioDeviceInfo>();

    public EngineManager(DeviceManager devices, SettingsStore settings, PresetStore presets)
    {
        _devices = devices;
        _settings = settings;
        _presets = presets;
        _refreshTimer = new Timer(_ => SafeRefresh("hot-plug"), null, Timeout.Infinite, Timeout.Infinite);
        _devices.DevicesChanged += (_, e) =>
        {
            // coalesce bursts of notifications (a USB headset raises several)
            _refreshTimer.Change(400, Timeout.Infinite);
        };
    }

    public IReadOnlyList<VoiceEngine> Engines
    {
        get { lock (_sync) return _engines.Values.OrderBy(e => e.DeviceName, StringComparer.OrdinalIgnoreCase).ToList(); }
    }

    public VoiceEngine? GetEngine(string deviceId)
    {
        lock (_sync) return _engines.GetValueOrDefault(deviceId);
    }

    /// <summary>Preset-independent clean-up settings shared by every microphone.</summary>
    public GlobalProcessing CurrentGlobal => new(Settings.NoiseReduction, Settings.VoiceIsolation);

    /// <summary>Updates the global noise suppression / voice isolation for all engines in real time.</summary>
    public void SetGlobalProcessing(float noiseReduction, float voiceIsolation)
    {
        Settings.NoiseReduction = Math.Clamp(noiseReduction, 0f, 1f);
        Settings.VoiceIsolation = Math.Clamp(voiceIsolation, 0f, 1f);
        var g = CurrentGlobal;
        foreach (var e in Engines) e.Global = g;
        _settings.SaveSoon();
    }

    public bool MasterEnabled
    {
        get => Settings.MasterEnabled;
        set
        {
            Settings.MasterEnabled = value;
            foreach (var e in Engines) e.MasterEnabled = value;
            _settings.SaveSoon();
            Log.Info("Manager", $"Master {(value ? "enabled" : "disabled")}");
            EnginesChanged?.Invoke();
        }
    }

    /// <summary>Initial device scan, engine creation and routing.</summary>
    public IReadOnlyList<InterferenceReport> Interference { get; private set; } = Array.Empty<InterferenceReport>();

    /// <summary>
    /// Current settings schema. v2: presets re-tuned for the pitch-synchronous engine;
    /// v3: library rebuilt around realistic voices (cartoon presets removed); v4: celebrity presets removed.
    /// </summary>
    public const int CurrentSchemaVersion = 4;

    // ───────────────────────────── Background audio bed ─────────────────────────────

    private BackgroundTrack? _background;
    private VoiceEngine? _backgroundEngine;

    public BackgroundTrack? Background => _background;
    /// <summary>The microphone whose channel carries the background bed (first running engine).</summary>
    public VoiceEngine? BackgroundEngine => _backgroundEngine;

    /// <summary>Loads (or reloads) the background file and attaches it to the primary running engine.</summary>
    public string? LoadBackgroundFile(string? path)
    {
        Settings.BackgroundFilePath = path;
        _settings.SaveSoon();
        DetachBackground();
        if (string.IsNullOrEmpty(path)) { EnginesChanged?.Invoke(); return null; }
        if (!File.Exists(path)) { Log.Warn("Background", $"File not found: {path}"); EnginesChanged?.Invoke(); return "File not found"; }
        AssignBackground(resumeAt: 0);
        EnginesChanged?.Invoke();
        return null;
    }

    public void SetBackgroundPlaying(bool playing)
    {
        Settings.BackgroundPlaying = playing;
        _settings.SaveSoon();
        if (_background == null && playing && !string.IsNullOrEmpty(Settings.BackgroundFilePath)) AssignBackground(resumeAt: 0);
        if (_background != null) _background.Playing = playing;
        EnginesChanged?.Invoke();
    }

    public void SetBackgroundVolume(float volume)
    {
        Settings.BackgroundVolume = Math.Clamp(volume, 0f, 1.5f);
        if (_background != null) _background.Volume = Settings.BackgroundVolume;
        _settings.SaveSoon();
    }

    public void SetBackgroundLoop(bool loop)
    {
        Settings.BackgroundLoop = loop;
        if (_background != null) _background.Loop = loop;
        _settings.SaveSoon();
    }

    public void SeekBackground(double seconds) => _background?.Seek(seconds);

    /// <summary>Global voice level on the microphone channel (independent of presets), applied live to every engine.</summary>
    public void SetVoiceVolume(float volume)
    {
        Settings.VoiceVolume = Math.Clamp(volume, 0f, 2f);
        foreach (var e in Engines) e.VoiceVolume = Settings.VoiceVolume;
        _settings.SaveSoon();
    }

    private void DetachBackground()
    {
        if (_backgroundEngine != null) _backgroundEngine.Background = null;
        _backgroundEngine = null;
        _background?.Dispose();
        _background = null;
    }

    /// <summary>Creates the track at the primary engine's sample rate and attaches it to that engine only.</summary>
    private void AssignBackground(double resumeAt)
    {
        var path = Settings.BackgroundFilePath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        var engine = Engines.FirstOrDefault(e => e.State == EngineState.Running);
        if (engine == null) return;
        try
        {
            var track = new BackgroundTrack(path, engine.SampleRate)
            {
                Volume = Settings.BackgroundVolume,
                Loop = Settings.BackgroundLoop,
                Playing = Settings.BackgroundPlaying
            };
            if (resumeAt > 0.05) track.Seek(resumeAt);
            _background = track;
            _backgroundEngine = engine;
            engine.Background = track;
            Log.Info("Background", $"Bed '{track.FileName}' attached to '{engine.DeviceName}'");
        }
        catch (Exception ex)
        {
            Log.Error("Background", "Could not start background track", ex);
        }
    }

    /// <summary>Keeps the bed on a running engine when engines start/stop (called after every refresh).</summary>
    private void ReassignBackgroundIfNeeded()
    {
        if (string.IsNullOrEmpty(Settings.BackgroundFilePath)) return;
        var current = _backgroundEngine;
        bool currentOk = current != null && current.State == EngineState.Running && ReferenceEquals(current.Background, _background) && _background != null;
        if (currentOk) return;
        double resume = _background?.PositionSeconds ?? 0;
        DetachBackground();
        AssignBackground(resume);
    }

    /// <summary>Upgrades persisted settings from older schemas (idempotent).</summary>
    private void MigrateSettings()
    {
        if (Settings.SchemaVersion >= CurrentSchemaVersion) return;
        // Built-in preset values changed: re-apply them so saved microphones do not keep stale parameters.
        // Microphones whose preset no longer exists fall back to the clean microphone preset.
        foreach (var mic in Settings.Microphones)
        {
            var preset = _presets.Find(mic.PresetId);
            if (preset == null && !string.IsNullOrEmpty(mic.PresetId) && mic.PresetId.StartsWith("builtin-"))
            {
                preset = _presets.Find(Settings.DefaultPresetId) ?? _presets.Find("builtin-clean-microphone");
                if (preset != null) { mic.PresetId = preset.Id; mic.PresetName = preset.Name; }
            }
            if (preset is { IsBuiltIn: true })
            {
                mic.Profile.CopyFrom(preset.Profile, notify: false);
                Log.Info("Manager", $"Migrated '{mic.DeviceName}' to preset '{preset.Name}' (schema {Settings.SchemaVersion} → {CurrentSchemaVersion})");
            }
        }
        Settings.FavoritePresetIds.RemoveAll(id => _presets.Find(id) == null);
        Settings.SchemaVersion = CurrentSchemaVersion;
        _settings.SaveSoon();
    }

    public void Initialize()
    {
        MigrateSettings();
        RefreshDevices();
        DetectCable();
        Interference = InterferenceDetector.Scan();
        InterferenceDetector.LogScan();
        lock (_sync)
        {
            foreach (var mic in CaptureDevices)
            {
                if (VirtualCableDetector.LooksVirtual(mic)) continue; // never process our own output
                EnsureEngine(mic, isNew: !Settings.Microphones.Any(m => m.DeviceId == mic.Id));
            }
        }
        foreach (var e in Engines)
        {
            if (e.Config.Active) StartEngine(e);
        }
        ApplyDefaultMicrophonePolicy();
        ReassignBackgroundIfNeeded();
        _settings.SaveSoon();
        EnginesChanged?.Invoke();
    }

    private void RefreshDevices()
    {
        CaptureDevices = _devices.GetCaptureDevices();
        RenderDevices = _devices.GetRenderDevices();
    }

    private void DetectCable()
    {
        DetectedCables = VirtualCableDetector.Detect(RenderDevices, CaptureDevices);
        VirtualCable? chosen = null;
        if (!string.IsNullOrEmpty(Settings.VirtualCableRenderId))
            chosen = DetectedCables.FirstOrDefault(c => c.RenderInput.Id == Settings.VirtualCableRenderId);
        if (chosen == null && !string.IsNullOrEmpty(Settings.VirtualCableRenderId))
        {
            // user picked a render device that is not detected as virtual — honour it anyway
            var r = RenderDevices.FirstOrDefault(d => d.Id == Settings.VirtualCableRenderId);
            if (r != null)
            {
                var c = CaptureDevices.FirstOrDefault(d => d.Id == Settings.VirtualCableCaptureId);
                chosen = new VirtualCable(r, c, "Custom");
            }
        }
        chosen ??= DetectedCables.FirstOrDefault();
        bool changed = chosen?.RenderInput.Id != ActiveCable?.RenderInput.Id || chosen?.CaptureOutput?.Id != ActiveCable?.CaptureOutput?.Id;
        ActiveCable = chosen;
        if (chosen != null)
            Log.Info("Manager", $"Virtual cable: {chosen.DisplayName}");
        else
            Log.Warn("Manager", "No virtual audio cable detected. Install VB-CABLE to route the processed voice to other applications.");
        if (changed) VirtualCableChanged?.Invoke();
    }

    /// <summary>Selects the virtual cable manually (render endpoint we write to, capture endpoint apps use).</summary>
    public void SelectVirtualCable(string? renderId, string? captureId)
    {
        Settings.VirtualCableRenderId = renderId;
        Settings.VirtualCableCaptureId = captureId;
        _settings.SaveSoon();
        DetectCable();
        foreach (var e in Engines) e.SetVirtualOutput(ResolveVirtualOutput(e));
        ApplyDefaultMicrophonePolicy();
        EnginesChanged?.Invoke();
    }

    private string? ResolveVirtualOutput(VoiceEngine e)
        => e.Config.VirtualOutputDeviceId ?? ActiveCable?.RenderInput.Id;

    private string? ResolveMonitorOutput()
        => Settings.MonitorDeviceId ?? _devices.GetDefaultDeviceId(DataFlow.Render, Role.Multimedia);

    private VoiceEngine EnsureEngine(AudioDeviceInfo mic, bool isNew)
    {
        if (_engines.TryGetValue(mic.Id, out var existing))
        {
            existing.IsPresent = true;
            return existing;
        }
        var cfg = Settings.GetOrCreate(mic.Id, mic.Name);
        if (isNew)
        {
            cfg.Active = Settings.AutoActivateNewMicrophones;
            var preset = _presets.Find(Settings.DefaultPresetId);
            if (preset != null)
            {
                cfg.Profile.CopyFrom(preset.Profile, notify: false);
                cfg.PresetId = preset.Id;
                cfg.PresetName = preset.Name;
            }
        }
        var engine = new VoiceEngine(_devices, cfg, Settings.BufferMilliseconds)
        {
            MasterEnabled = Settings.MasterEnabled,
            Global = CurrentGlobal,
            VoiceVolume = Settings.VoiceVolume
        };
        engine.Profile.PropertyChanged += (_, _) => _settings.SaveSoon();
        engine.StateChanged += _ => EnginesChanged?.Invoke();
        _engines[mic.Id] = engine;
        Log.Info("Manager", $"Microphone registered: {mic.Name} (active={cfg.Active})");
        return engine;
    }

    public bool StartEngine(VoiceEngine e)
    {
        e.Config.Active = true;
        bool ok = e.Start(ResolveVirtualOutput(e), ResolveMonitorOutput(), e.Config.MonitorEnabled);
        ReassignBackgroundIfNeeded();
        _settings.SaveSoon();
        EnginesChanged?.Invoke();
        return ok;
    }

    public void StopEngine(VoiceEngine e, bool deactivate = true)
    {
        if (deactivate) e.Config.Active = false;
        if (ReferenceEquals(e, _backgroundEngine)) { e.Background = null; }
        e.Stop();
        ReassignBackgroundIfNeeded();
        _settings.SaveSoon();
        EnginesChanged?.Invoke();
    }

    public void RestartEngine(VoiceEngine e)
    {
        e.Stop();
        StartEngine(e);
    }

    /// <summary>Restarts all running engines (used after changing the buffer size).</summary>
    public void RestartAll()
    {
        foreach (var e in Engines.Where(x => x.State == EngineState.Running))
        {
            e.Stop();
            var cfg = e.Config;
            lock (_sync)
            {
                _engines.Remove(e.DeviceId);
                e.Dispose();
                var mic = CaptureDevices.FirstOrDefault(d => d.Id == cfg.DeviceId);
                if (mic != null) EnsureEngine(mic, false);
            }
            var fresh = GetEngine(cfg.DeviceId);
            if (fresh != null) StartEngine(fresh);
        }
        ReassignBackgroundIfNeeded();
        EnginesChanged?.Invoke();
    }

    public void SetMonitorDevice(string? deviceId)
    {
        Settings.MonitorDeviceId = deviceId;
        _settings.SaveSoon();
        foreach (var e in Engines) e.SetMonitor(ResolveMonitorOutput(), e.Config.MonitorEnabled);
    }

    public void SetMonitoring(VoiceEngine e, bool enabled)
    {
        e.SetMonitor(ResolveMonitorOutput(), enabled);
        _settings.SaveSoon();
    }

    public void ApplyPreset(VoiceEngine e, VoicePreset preset)
    {
        e.Profile.CopyFrom(preset.Profile);
        e.Config.PresetId = preset.Id;
        e.Config.PresetName = preset.Name;
        _settings.SaveSoon();
        Log.Info("Manager", $"Applied preset '{preset.Name}' to '{e.DeviceName}'");
        EnginesChanged?.Invoke();
    }

    public void ApplyPresetToAll(VoicePreset preset)
    {
        foreach (var e in Engines) ApplyPreset(e, preset);
        Notify("Preset applied", $"'{preset.Name}' applied to all microphones");
    }

    /// <summary>Copies the working profile of one engine to all other engines.</summary>
    public void CopyProfileToAll(VoiceEngine source)
    {
        foreach (var e in Engines)
        {
            if (ReferenceEquals(e, source)) continue;
            e.Profile.CopyFrom(source.Profile);
            e.Config.PresetId = source.Config.PresetId;
            e.Config.PresetName = source.Config.PresetName;
        }
        _settings.SaveSoon();
        EnginesChanged?.Invoke();
    }

    private void SafeRefresh(string reason)
    {
        try { Refresh(reason); }
        catch (Exception ex) { Log.Error("Manager", "Device refresh failed", ex); }
    }

    /// <summary>Re-scans devices: starts engines for new microphones, stops engines whose microphone vanished.</summary>
    public void Refresh(string reason = "manual")
    {
        if (_disposed) return;
        Log.Debug("Manager", $"Refreshing devices ({reason})");
        RefreshDevices();
        var previousCable = ActiveCable?.RenderInput.Id;
        DetectCable();

        var present = CaptureDevices.Where(d => !VirtualCableDetector.LooksVirtual(d)).ToDictionary(d => d.Id);
        var toStart = new List<VoiceEngine>();
        var toRemove = new List<VoiceEngine>();
        lock (_sync)
        {
            foreach (var mic in present.Values)
            {
                bool known = _engines.ContainsKey(mic.Id);
                bool isNew = !Settings.Microphones.Any(m => m.DeviceId == mic.Id);
                var e = EnsureEngine(mic, isNew);
                if (!known)
                {
                    Notify("Microphone connected", mic.Name);
                    if (e.Config.Active) toStart.Add(e);
                }
                else if (e.Config.Active && e.State != EngineState.Running)
                {
                    toStart.Add(e); // device came back after being unplugged
                }
            }
            foreach (var e in _engines.Values.ToList())
            {
                if (!present.ContainsKey(e.DeviceId))
                {
                    toRemove.Add(e);
                    _engines.Remove(e.DeviceId);
                }
            }
        }
        foreach (var e in toRemove)
        {
            Log.Info("Manager", $"Microphone removed: {e.DeviceName}");
            Notify("Microphone disconnected", e.DeviceName, isWarning: true);
            e.Stop();
            e.Dispose();
        }
        foreach (var e in toStart)
        {
            if (e.Start(ResolveVirtualOutput(e), ResolveMonitorOutput(), e.Config.MonitorEnabled))
                Log.Info("Manager", $"Auto-started '{e.DeviceName}'");
        }

        // Re-route engines whose virtual output disappeared or whose cable changed
        foreach (var e in Engines.Where(x => x.State == EngineState.Running))
        {
            var want = ResolveVirtualOutput(e);
            if (e.VirtualOutputId != want) e.SetVirtualOutput(want);
            if (e.Config.MonitorEnabled && !e.MonitorActive) e.SetMonitor(ResolveMonitorOutput(), true);
        }

        if (previousCable != ActiveCable?.RenderInput.Id)
        {
            if (ActiveCable == null) Notify("Virtual cable missing", "The virtual audio cable is no longer available. Other apps will not receive your processed voice.", true);
            ApplyDefaultMicrophonePolicy();
        }
        ReassignBackgroundIfNeeded();
        _settings.SaveSoon();
        EnginesChanged?.Invoke();
    }

    /// <summary>Makes the virtual cable the Windows default microphone (if configured) so every app uses the processed voice.</summary>
    public void ApplyDefaultMicrophonePolicy()
    {
        if (!Settings.AutoSetDefaultMicrophone) return;
        var target = ActiveCable?.CaptureOutput?.Id;
        if (string.IsNullOrEmpty(target)) return;
        var currentDefault = _devices.GetDefaultDeviceId(DataFlow.Capture, Role.Multimedia);
        var currentCom = _devices.GetDefaultDeviceId(DataFlow.Capture, Role.Communications);
        if (currentDefault == target && currentCom == target) return;
        if (!_defaultOverridden)
        {
            _originalDefaultMic = currentDefault;
            _originalDefaultComMic = currentCom;
        }
        if (PolicyConfig.SetDefaultEndpoint(target))
        {
            _defaultOverridden = true;
            Log.Info("Manager", $"Windows default microphone set to virtual cable ({ActiveCable!.CaptureOutput!.Name})");
        }
    }

    /// <summary>Restores the default microphone that was active before the app took over.</summary>
    public void RestoreDefaultMicrophone()
    {
        if (!_defaultOverridden) return;
        var restore = _originalDefaultMic;
        if (string.IsNullOrEmpty(restore) || _devices.GetDevice(restore) == null)
        {
            // original vanished: pick the first physical microphone
            restore = CaptureDevices.FirstOrDefault(d => !VirtualCableDetector.LooksVirtual(d))?.Id;
        }
        if (!string.IsNullOrEmpty(restore) && PolicyConfig.SetDefaultEndpoint(restore))
        {
            Log.Info("Manager", "Windows default microphone restored");
            _defaultOverridden = false;
        }
    }

    public bool IsDefaultMicrophoneOverridden => _defaultOverridden;

    private void Notify(string title, string message, bool isWarning = false)
    {
        try { Notification?.Invoke(this, new NotificationEventArgs(title, message, isWarning)); }
        catch (Exception ex) { Log.Error("Manager", "Notification handler threw", ex); }
    }

    public void Shutdown()
    {
        if (_disposed) return;
        _refreshTimer.Change(Timeout.Infinite, Timeout.Infinite);
        try { DetachBackground(); } catch { /* ignore */ }
        foreach (var e in Engines)
        {
            try { e.Stop(); e.Dispose(); } catch { /* ignore */ }
        }
        if (Settings.RestoreDefaultOnExit) RestoreDefaultMicrophone();
        _settings.Flush();
    }

    public void Dispose()
    {
        if (_disposed) return;
        Shutdown();
        _disposed = true;
        _refreshTimer.Dispose();
    }
}
