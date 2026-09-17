using System.Text.Json.Serialization;

namespace VoiceChanger.Core.Presets;

/// <summary>Preset categories used for grouping in the browser.</summary>
public static class PresetCategories
{
    public const string Female = "Female Voices";
    public const string Male = "Male Voices";
    public const string Age = "Age";
    public const string Professional = "Professional";
    public const string Accent = "Accent Flavor";
    public const string Utility = "Utility";
    public const string Custom = "My Presets";

    public static readonly string[] All =
    {
        Female, Male, Age, Professional, Accent, Utility, Custom
    };
}

/// <summary>A named, categorised voice profile.</summary>
public sealed class VoicePreset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Untitled";
    public string Category { get; set; } = PresetCategories.Custom;
    public string Description { get; set; } = string.Empty;
    public string[] Tags { get; set; } = Array.Empty<string>();
    /// <summary>Emoji or short glyph used as the preset badge in the UI.</summary>
    public string Icon { get; set; } = "🎙";
    public bool IsBuiltIn { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
    public int SchemaVersion { get; set; } = 1;
    public VoiceProfile Profile { get; set; } = new();

    [JsonIgnore]
    public string SearchText => $"{Name} {Category} {Description} {string.Join(' ', Tags)}".ToLowerInvariant();

    public VoicePreset Clone() => new()
    {
        Id = Id,
        Name = Name,
        Category = Category,
        Description = Description,
        Tags = (string[])Tags.Clone(),
        Icon = Icon,
        IsBuiltIn = IsBuiltIn,
        CreatedUtc = CreatedUtc,
        ModifiedUtc = ModifiedUtc,
        SchemaVersion = SchemaVersion,
        Profile = Profile.Clone()
    };
}
