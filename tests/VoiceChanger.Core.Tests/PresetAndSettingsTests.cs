using System.Text.Json;
using VoiceChanger.Core.Presets;
using VoiceChanger.Core.Settings;
using Xunit;

namespace VoiceChanger.Core.Tests;

public class PresetAndSettingsTests
{
    [Fact]
    public void BuiltInLibrary_HasAtLeastFortyPresets_WithUniqueIdsAndNames()
    {
        var all = BuiltInPresets.All;
        Assert.True(all.Count >= 40, $"only {all.Count} built-in presets");
        Assert.Equal(all.Count, all.Select(p => p.Id).Distinct().Count());
        Assert.Equal(all.Count, all.Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(all, p =>
        {
            Assert.StartsWith("builtin-", p.Id);
            Assert.True(p.IsBuiltIn);
            Assert.Contains(p.Category, PresetCategories.All);
            Assert.False(string.IsNullOrWhiteSpace(p.Description));
            Assert.True(p.Tags.Length >= 1);
            Assert.True(p.Profile.LimiterEnabled, $"{p.Name} must keep the limiter on");
        });
    }

    [Fact]
    public void BuiltInLibrary_CoversRequestedCategories()
    {
        var cats = BuiltInPresets.All.GroupBy(p => p.Category).ToDictionary(g => g.Key, g => g.Count());
        Assert.True(cats[PresetCategories.Female] >= 14);
        Assert.True(cats[PresetCategories.Male] >= 14);
        Assert.True(cats[PresetCategories.Age] >= 6);
        Assert.True(cats[PresetCategories.Professional] >= 6);
        Assert.DoesNotContain(BuiltInPresets.All, p => p.Name.Contains("Style") && p.Tags.Contains("celebrity-style"));
        Assert.True(cats[PresetCategories.Accent] >= 5);
        Assert.True(cats[PresetCategories.Utility] >= 3);
        Assert.NotNull(BuiltInPresets.All.FirstOrDefault(p => p.Id == "builtin-clean-microphone"));
    }

    [Fact]
    public void BuiltInLibrary_IsRealistic_NoCartoonEffects()
    {
        foreach (var p in BuiltInPresets.All)
        {
            var v = p.Profile;
            Assert.True(v.PitchSemitones is >= -5 and <= 8.5f, $"{p.Name}: pitch {v.PitchSemitones}");
            Assert.True(v.FormantSemitones is >= -2.6f and <= 4.1f, $"{p.Name}: formant {v.FormantSemitones}");
            Assert.True(v.RobotMix == 0 && v.ChorusMix == 0 && v.Crush == 0 && v.EchoMix == 0, $"{p.Name}: cartoon effect enabled");
            Assert.True(v.ReverbMix <= 0.1f, $"{p.Name}: reverb {v.ReverbMix}");
            Assert.True(v.RadioMix == 0 || p.Name.Contains("Transatlantic"), $"{p.Name}: radio effect enabled");
        }
    }

    [Fact]
    public void Profile_CopyFrom_RaisesSingleAggregateNotification_AndValueEquals()
    {
        var src = new VoiceProfile { PitchSemitones = 5, FormantSemitones = 2, ReverbMix = 0.3f };
        var dst = new VoiceProfile();
        int notifications = 0;
        dst.PropertyChanged += (_, e) => { notifications++; Assert.Equal(string.Empty, e.PropertyName); };
        dst.CopyFrom(src);
        Assert.Equal(1, notifications);
        Assert.True(dst.ValueEquals(src));
        Assert.False(dst.IsNeutral);
        dst.Reset();
        Assert.True(dst.IsNeutral);
    }

    [Fact]
    public void Profile_ClampsOutOfRangeValues()
    {
        var p = new VoiceProfile { PitchSemitones = 99, ReverbMix = -3, GateThresholdDb = float.NaN };
        Assert.Equal(24, p.PitchSemitones);
        Assert.Equal(0, p.ReverbMix);
        Assert.Equal(-80, p.GateThresholdDb);
    }

    [Fact]
    public void Preset_JsonRoundTrip_PreservesValues()
    {
        var preset = BuiltInPresets.All.First(p => p.Profile.PitchSemitones != 0);
        var json = JsonSerializer.Serialize(preset, SettingsStore.JsonOptions);
        var back = JsonSerializer.Deserialize<VoicePreset>(json, SettingsStore.JsonOptions)!;
        Assert.Equal(preset.Id, back.Id);
        Assert.Equal(preset.Name, back.Name);
        Assert.True(back.Profile.ValueEquals(preset.Profile));
    }

    [Fact]
    public void PresetStore_SaveDeleteImportExport_WorkOnDisk()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vcp-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new PresetStore(dir);
            Assert.Empty(store.User);
            var mine = new VoicePreset { Name = "My Voice", Profile = new VoiceProfile { PitchSemitones = -3 } };
            var saved = store.Save(mine);
            Assert.Single(store.User);
            Assert.False(saved.IsBuiltIn);

            // saving a built-in creates an editable copy instead of overwriting
            var copy = store.Save(BuiltInPresets.All[0]);
            Assert.NotEqual(BuiltInPresets.All[0].Id, copy.Id);
            Assert.Equal(2, store.User.Count);

            var export = Path.Combine(Path.GetTempPath(), "vcp-export-" + Guid.NewGuid().ToString("N") + ".json");
            store.Export(saved, export);
            var imported = store.Import(export);
            Assert.Single(imported);
            Assert.NotEqual(saved.Id, imported[0].Id); // id collision → new id
            Assert.Equal(3, store.User.Count);

            // reload from disk
            var store2 = new PresetStore(dir);
            Assert.Equal(3, store2.User.Count);
            Assert.True(store2.Delete(saved.Id));
            Assert.Equal(2, store2.User.Count);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void SettingsStore_RoundTrips_MicrophoneConfigs()
    {
        var file = Path.Combine(Path.GetTempPath(), "vcp-settings-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new SettingsStore(file);
            Assert.True(store.Settings.IsFirstRun);
            var mic = store.Settings.GetOrCreate("{dev-1}", "USB Mic");
            mic.Active = true;
            mic.Profile.PitchSemitones = 4;
            store.Settings.FavoritePresetIds.Add("builtin-kid");
            store.SaveSoon();
            store.Flush();

            var store2 = new SettingsStore(file);
            Assert.False(store2.Settings.IsFirstRun);
            var back = store2.Settings.Microphones.Single();
            Assert.Equal("USB Mic", back.DeviceName);
            Assert.True(back.Active);
            Assert.Equal(4, back.Profile.PitchSemitones);
            Assert.Contains("builtin-kid", store2.Settings.FavoritePresetIds);
        }
        finally
        {
            try { File.Delete(file); } catch { }
        }
    }
}
