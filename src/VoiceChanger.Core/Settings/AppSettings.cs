using System.Text.Json;
using System.Text.Json.Serialization;
using VoiceChanger.Core.Logging;
using VoiceChanger.Core.Presets;

namespace VoiceChanger.Core.Settings;

/// <summary>Per-microphone persisted configuration, keyed by the WASAPI endpoint ID.</summary>
public sealed class MicrophoneConfig
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    /// <summary>Whether the engine should run for this microphone whenever it is present.</summary>
    public bool Active { get; set; }
    /// <summary>Whether the voice effect is applied (false = transparent pass-through to the virtual cable).</summary>
    public bool EffectEnabled { get; set; } = true;
    /// <summary>ID of the preset that was last applied (for display); the working profile below is authoritative.</summary>
    public string? PresetId { get; set; }
    public string? PresetName { get; set; }
    /// <summary>Current working parameters (may be modified from the preset).</summary>
    public VoiceProfile Profile { get; set; } = new();
    /// <summary>Render endpoint that receives the processed audio (virtual cable input). Null = use the global default.</summary>
    public string? VirtualOutputDeviceId { get; set; }
    public bool MonitorEnabled { get; set; }
    public float MonitorVolume { get; set; } = 0.8f;
}

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Global master switch. When off, every engine passes audio through unprocessed.</summary>
    public bool MasterEnabled { get; set; } = true;

    /// <summary>Default virtual cable render endpoint ID (auto-detected when null).</summary>
    public string? VirtualCableRenderId { get; set; }
    /// <summary>Capture endpoint of the virtual cable (what other apps should use as microphone).</summary>
    public string? VirtualCableCaptureId { get; set; }

    /// <summary>Render endpoint used for live monitoring (headphones). Null = system default.</summary>
    public string? MonitorDeviceId { get; set; }

    /// <summary>Automatically make the virtual cable the Windows default microphone while running.</summary>
    public bool AutoSetDefaultMicrophone { get; set; } = true;
    /// <summary>Restore the original default microphone when the app exits.</summary>
    public bool RestoreDefaultOnExit { get; set; } = true;

    /// <summary>Automatically start processing newly connected microphones with the default preset.</summary>
    public bool AutoActivateNewMicrophones { get; set; } = true;
    /// <summary>Preset applied to newly detected microphones.</summary>
    public string DefaultPresetId { get; set; } = "builtin-clean-microphone";

    /// <summary>Global background-noise suppression amount (0..1). Independent of presets, applies to every microphone.</summary>
    public float NoiseReduction { get; set; } = 0.5f;
    /// <summary>Global voice isolation / background-talker reduction amount (0..1).</summary>
    public float VoiceIsolation { get; set; } = 0f;

    /// <summary>Background audio bed (music / ambience) mixed unprocessed into the microphone channel.</summary>
    public string? BackgroundFilePath { get; set; }
    public float BackgroundVolume { get; set; } = 0.5f;
    public bool BackgroundLoop { get; set; } = true;
    public bool BackgroundPlaying { get; set; }
    /// <summary>Global level of the (processed) voice on the microphone channel, independent of presets. 1 = unity.</summary>
    public float VoiceVolume { get; set; } = 1f;

    public int BufferMilliseconds { get; set; } = 20;
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;
    public string Theme { get; set; } = "Dark";

    public List<string> FavoritePresetIds { get; set; } = new();
    public List<MicrophoneConfig> Microphones { get; set; } = new();

    public double WindowWidth { get; set; } = 1280;
    public double WindowHeight { get; set; } = 780;
    public bool WindowMaximized { get; set; }

    [JsonIgnore]
    public bool IsFirstRun { get; set; }

    public MicrophoneConfig GetOrCreate(string deviceId, string name)
    {
        var m = Microphones.FirstOrDefault(x => x.DeviceId == deviceId);
        if (m == null)
        {
            m = new MicrophoneConfig { DeviceId = deviceId, DeviceName = name };
            Microphones.Add(m);
        }
        else if (!string.IsNullOrEmpty(name))
        {
            m.DeviceName = name;
        }
        return m;
    }
}

/// <summary>Loads and saves <see cref="AppSettings"/> atomically as JSON with debounced writes.</summary>
public sealed class SettingsStore : IDisposable
{
    private readonly string _path;
    private readonly Timer _debounce;
    private volatile bool _dirty;
    private readonly object _lock = new();

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public AppSettings Settings { get; private set; } = new();

    public SettingsStore(string? path = null)
    {
        _path = path ?? AppPaths.SettingsFile;
        _debounce = new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                Settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
                Log.Info("Settings", $"Loaded settings from {_path} ({Settings.Microphones.Count} microphone configs)");
            }
            else
            {
                Settings = new AppSettings { IsFirstRun = true };
                Log.Info("Settings", "No settings file found; using defaults (first run)");
            }
        }
        catch (Exception ex)
        {
            Log.Error("Settings", "Failed to load settings; backing up and using defaults", ex);
            try { File.Copy(_path, _path + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true); } catch { /* ignore */ }
            Settings = new AppSettings { IsFirstRun = true };
        }
    }

    /// <summary>Schedules a save shortly in the future (coalesces bursts of changes).</summary>
    public void SaveSoon()
    {
        _dirty = true;
        _debounce.Change(600, Timeout.Infinite);
    }

    /// <summary>Writes immediately if there are pending changes.</summary>
    public void Flush()
    {
        if (!_dirty) return;
        lock (_lock)
        {
            try
            {
                _dirty = false;
                var json = JsonSerializer.Serialize(Settings, JsonOptions);
                var tmp = _path + ".tmp";
                File.WriteAllText(tmp, json);
                File.Move(tmp, _path, true);
            }
            catch (Exception ex)
            {
                Log.Error("Settings", "Failed to save settings", ex);
                _dirty = true;
            }
        }
    }

    public void Dispose()
    {
        _debounce.Dispose();
        Flush();
    }
}
