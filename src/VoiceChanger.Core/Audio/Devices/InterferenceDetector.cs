using Microsoft.Win32;
using VoiceChanger.Core.Logging;

namespace VoiceChanger.Core.Audio.Devices;

/// <summary>A third-party component known to intercept or silence microphone capture system-wide.</summary>
public sealed record InterferenceReport(string Product, string Component, string Detail, string Remedy, bool IsClassFilter = false);

/// <summary>
/// Detects other voice-changer / virtual-audio drivers that hook the audio device class or run as
/// kernel filters. Such drivers (e.g. NCH Voxal) intercept every microphone and output silence when
/// their own application is not running, which looks like "the microphone is dead" to every other app.
/// Read-only registry checks; no elevation required.
/// </summary>
public static class InterferenceDetector
{
    private const string MediaClassKey = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e96c-e325-11ce-bfc1-08002be10318}";
    private const string ServicesKey = @"SYSTEM\CurrentControlSet\Services\";

    /// <summary>Windows in-box filters that are expected on the MEDIA class.</summary>
    private static readonly HashSet<string> BenignClassFilters = new(StringComparer.OrdinalIgnoreCase) { "ksthunk" };

    private static readonly (string Service, string Product, string Remedy)[] KnownDrivers =
    {
        ("voxaldriver", "NCH Voxal Voice Changer",
            "Uninstall Voxal (Settings › Apps), or remove 'voxaldriver' from the audio class UpperFilters, then restart Windows."),
        ("ScreamBAudioSvc", "Screaming Bee (MorphVOX) audio driver",
            "If MorphVOX is not needed, uninstall MorphVOX Pro so its driver no longer loads."),
        ("vcs", "AV Voice Changer Software Diamond (Avnex) driver",
            "Uninstall AV Voice Changer Software if it is no longer used."),
        ("VCSVADHWSer", "AV Voice Changer virtual audio device",
            "Uninstall AV Voice Changer Software if it is no longer used."),
    };

    public static IReadOnlyList<InterferenceReport> Scan()
    {
        var result = new List<InterferenceReport>();
        try
        {
            using var cls = Registry.LocalMachine.OpenSubKey(MediaClassKey);
            foreach (var valueName in new[] { "UpperFilters", "LowerFilters" })
            {
                if (cls?.GetValue(valueName) is string[] filters)
                {
                    foreach (var f in filters.Where(f => !string.IsNullOrWhiteSpace(f) && !BenignClassFilters.Contains(f)))
                    {
                        var known = KnownDrivers.FirstOrDefault(k => k.Service.Equals(f, StringComparison.OrdinalIgnoreCase));
                        result.Add(new InterferenceReport(
                            known.Product ?? $"Unknown audio class filter '{f}'",
                            f,
                            $"'{f}' is installed as a {valueName} filter on the Windows audio (MEDIA) device class, so it sits in the capture path of every microphone" +
                            (IsServiceRunning(f) ? " and is currently loaded." : "."),
                            known.Remedy ?? $"Remove '{f}' from the audio class {valueName} registry value (administrator) and restart, or uninstall the product that installed it.",
                            IsClassFilter: true));
                    }
                }
            }
        }
        catch (Exception ex) { Log.Warn("Interference", "Class filter scan failed: " + ex.Message); }

        foreach (var (service, product, remedy) in KnownDrivers)
        {
            if (result.Any(r => r.Component.Equals(service, StringComparison.OrdinalIgnoreCase))) continue;
            if (IsServiceRunning(service))
                result.Add(new InterferenceReport(product, service, $"Kernel driver service '{service}' is loaded.", remedy));
        }
        return result;
    }

    private static bool IsServiceRunning(string service)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(ServicesKey + service);
            if (key == null) return false;
            // Start: 0 boot, 1 system, 2 auto, 3 demand, 4 disabled. Anything not disabled counts as active
            // for a class filter because PnP loads filters on demand.
            return key.GetValue("Start") is int start && start != 4;
        }
        catch { return false; }
    }

    public static void LogScan()
    {
        var reports = Scan();
        if (reports.Count == 0) { Log.Info("Interference", "No interfering audio drivers detected"); return; }
        foreach (var r in reports) Log.Warn("Interference", $"{r.Product} [{r.Component}]: {r.Detail} Remedy: {r.Remedy}");
    }
}
