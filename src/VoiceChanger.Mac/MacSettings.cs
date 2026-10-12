using System.Text.Json;
using VoiceChanger.Core.Logging;
using VoiceChanger.Core.Presets;

namespace VoiceChanger.Mac;

/// <summary>Everything the Mac app remembers between launches (~/Library/Application Support/VoiceChangerPro/settings.json).</summary>
public sealed class MacSettings
{
    public string? MicrophoneName { get; set; }
    public string? CableName { get; set; }
    public string? MonitorName { get; set; }
    public bool MonitorEnabled { get; set; }
    public float MonitorVolume { get; set; } = 0.8f;
    public bool MasterEnabled { get; set; } = true;
    public string PresetId { get; set; } = "builtin-clean-microphone";
    public VoiceProfile Profile { get; set; } = new();
    public float NoiseReduction { get; set; } = 0.5f;
    public float VoiceIsolation { get; set; }
    public float VoiceVolume { get; set; } = 1f;
    /// <summary>Make the virtual cable the Mac's default microphone while running, so every app hears the changed voice.</summary>
    public bool AutoSetDefaultMicrophone { get; set; } = true;
    /// <summary>Start processing automatically when the app opens.</summary>
    public bool StartOnLaunch { get; set; } = true;

    private static string FilePath => Path.Combine(Program.SupportDirectory, "settings.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static MacSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<MacSettings>(File.ReadAllText(FilePath), Options) ?? new MacSettings();
        }
        catch (Exception ex) { Log.Error("Settings", "Could not read settings, using defaults", ex); }
        var s = new MacSettings();
        var preset = BuiltInPresets.All.FirstOrDefault(p => p.Id == s.PresetId);
        if (preset != null) s.Profile = preset.Profile.Clone();
        return s;
    }

    public void Save()
    {
        try
        {
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, Options));
            File.Move(tmp, FilePath, overwrite: true);
        }
        catch (Exception ex) { Log.Error("Settings", "Could not save settings", ex); }
    }
}
