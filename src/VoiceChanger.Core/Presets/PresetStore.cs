using System.Text.Json;
using VoiceChanger.Core.Logging;
using VoiceChanger.Core.Settings;

namespace VoiceChanger.Core.Presets;

/// <summary>
/// Manages the preset library: read-only built-ins plus user presets persisted one-per-file
/// under %AppData%\VoiceChangerPro\presets. Supports import/export of JSON preset files.
/// </summary>
public sealed class PresetStore
{
    private readonly string _dir;
    private readonly List<VoicePreset> _user = new();
    private readonly object _lock = new();

    public event Action? Changed;

    public PresetStore(string? directory = null)
    {
        _dir = directory ?? AppPaths.Presets;
        Load();
    }

    public IReadOnlyList<VoicePreset> BuiltIn => BuiltInPresets.All;

    public IReadOnlyList<VoicePreset> User
    {
        get { lock (_lock) return _user.ToArray(); }
    }

    public IEnumerable<VoicePreset> All => BuiltIn.Concat(User);

    public VoicePreset? Find(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        var b = BuiltInPresets.All.FirstOrDefault(p => p.Id == id);
        if (b != null) return b;
        lock (_lock) return _user.FirstOrDefault(p => p.Id == id);
    }

    public void Load()
    {
        lock (_lock)
        {
            _user.Clear();
            try
            {
                Directory.CreateDirectory(_dir);
                foreach (var file in Directory.GetFiles(_dir, "*.json"))
                {
                    try
                    {
                        var preset = JsonSerializer.Deserialize<VoicePreset>(File.ReadAllText(file), SettingsStore.JsonOptions);
                        if (preset == null) continue;
                        preset.IsBuiltIn = false;
                        if (string.IsNullOrWhiteSpace(preset.Category)) preset.Category = PresetCategories.Custom;
                        _user.Add(preset);
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("Presets", $"Skipping unreadable preset file {file}: {ex.Message}");
                    }
                }
                _user.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                Log.Info("Presets", $"Loaded {_user.Count} user presets and {BuiltIn.Count} built-in presets");
            }
            catch (Exception ex)
            {
                Log.Error("Presets", "Failed to load user presets", ex);
            }
        }
    }

    /// <summary>Saves a user preset (new or existing). Built-in presets cannot be overwritten; a copy is created instead.</summary>
    public VoicePreset Save(VoicePreset preset)
    {
        lock (_lock)
        {
            if (preset.IsBuiltIn || BuiltInPresets.All.Any(p => p.Id == preset.Id))
            {
                preset = preset.Clone();
                preset.Id = Guid.NewGuid().ToString("N");
                preset.IsBuiltIn = false;
            }
            preset.ModifiedUtc = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(preset.Category)) preset.Category = PresetCategories.Custom;
            var existing = _user.FindIndex(p => p.Id == preset.Id);
            if (existing >= 0) _user[existing] = preset; else _user.Add(preset);
            WriteFile(preset);
            _user.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        }
        Changed?.Invoke();
        return preset;
    }

    public bool Delete(string id)
    {
        bool removed;
        lock (_lock)
        {
            var idx = _user.FindIndex(p => p.Id == id);
            if (idx < 0) return false;
            _user.RemoveAt(idx);
            try { File.Delete(PathFor(id)); } catch (Exception ex) { Log.Warn("Presets", $"Could not delete preset file: {ex.Message}"); }
            removed = true;
        }
        Changed?.Invoke();
        return removed;
    }

    public void Export(VoicePreset preset, string filePath)
    {
        File.WriteAllText(filePath, JsonSerializer.Serialize(preset, SettingsStore.JsonOptions));
        Log.Info("Presets", $"Exported preset '{preset.Name}' to {filePath}");
    }

    public void ExportAll(string filePath)
    {
        File.WriteAllText(filePath, JsonSerializer.Serialize(User, SettingsStore.JsonOptions));
    }

    /// <summary>Imports one preset or an array of presets from a JSON file. Returns the imported presets.</summary>
    public IReadOnlyList<VoicePreset> Import(string filePath)
    {
        var json = File.ReadAllText(filePath).TrimStart();
        var list = new List<VoicePreset>();
        if (json.StartsWith('['))
            list.AddRange(JsonSerializer.Deserialize<List<VoicePreset>>(json, SettingsStore.JsonOptions) ?? new());
        else
        {
            var one = JsonSerializer.Deserialize<VoicePreset>(json, SettingsStore.JsonOptions);
            if (one != null) list.Add(one);
        }
        var result = new List<VoicePreset>();
        foreach (var p in list)
        {
            p.IsBuiltIn = false;
            if (Find(p.Id) != null) p.Id = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(p.Category)) p.Category = PresetCategories.Custom;
            result.Add(Save(p));
        }
        Log.Info("Presets", $"Imported {result.Count} preset(s) from {filePath}");
        return result;
    }

    private string PathFor(string id) => Path.Combine(_dir, SafeFileName(id) + ".json");

    private void WriteFile(VoicePreset preset)
    {
        try
        {
            Directory.CreateDirectory(_dir);
            var path = PathFor(preset.Id);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(preset, SettingsStore.JsonOptions));
            File.Move(tmp, path, true);
        }
        catch (Exception ex)
        {
            Log.Error("Presets", $"Failed to write preset '{preset.Name}'", ex);
        }
    }

    private static string SafeFileName(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }
}
