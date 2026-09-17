namespace VoiceChanger.Core.Audio.Devices;

/// <summary>A virtual audio cable: the render endpoint we write into and the capture endpoint other apps read from.</summary>
public sealed record VirtualCable(AudioDeviceInfo RenderInput, AudioDeviceInfo? CaptureOutput, string Vendor)
{
    public string DisplayName => $"{Vendor}: {RenderInput.Name}" + (CaptureOutput != null ? $"  →  {CaptureOutput.Name}" : "  (no paired microphone endpoint found)");
}

/// <summary>
/// Detects installed virtual audio cable drivers (VB-CABLE, Virtual Audio Cable, VoiceMeeter,
/// Screaming Bee, Steelseries Sonar, NVIDIA Broadcast etc.) and pairs their render and capture endpoints.
/// </summary>
public static class VirtualCableDetector
{
    private static readonly (string Match, string Vendor)[] KnownVendors =
    {
        ("VB-Audio", "VB-CABLE"),
        ("CABLE Input", "VB-CABLE"),
        ("CABLE Output", "VB-CABLE"),
        ("VoiceMeeter", "VoiceMeeter"),
        ("Virtual Audio Cable", "Virtual Audio Cable"),
        ("Screaming Bee", "Screaming Bee"),
        ("Sonar", "SteelSeries Sonar"),
        ("NVIDIA Broadcast", "NVIDIA Broadcast"),
        ("Virtual Cable", "Virtual Cable"),
        ("Virtual", "Virtual device"),
    };

    public const string VbCableDownloadUrl = "https://vb-audio.com/Cable/";

    public static bool LooksVirtual(AudioDeviceInfo d)
    {
        var text = d.Name + " " + d.AdapterName;
        foreach (var (match, _) in KnownVendors)
            if (text.Contains(match, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static string VendorOf(AudioDeviceInfo d)
    {
        var text = d.Name + " " + d.AdapterName;
        foreach (var (match, vendor) in KnownVendors)
            if (text.Contains(match, StringComparison.OrdinalIgnoreCase)) return vendor;
        return "Virtual device";
    }

    /// <summary>Finds all virtual cables, pairing each virtual render endpoint with its most likely capture endpoint.</summary>
    public static IReadOnlyList<VirtualCable> Detect(IReadOnlyList<AudioDeviceInfo> renderDevices, IReadOnlyList<AudioDeviceInfo> captureDevices)
    {
        var result = new List<VirtualCable>();
        var virtualCaptures = captureDevices.Where(LooksVirtual).ToList();
        foreach (var r in renderDevices.Where(LooksVirtual))
        {
            var pair = FindPair(r, virtualCaptures);
            result.Add(new VirtualCable(r, pair, VendorOf(r)));
        }
        // Prefer real cables (with a paired capture endpoint) and VB-CABLE first.
        // Prefer real cables (with a paired capture endpoint), VB-CABLE first, and the standard
        // 2-channel "CABLE Input" over the 8/16-channel variants that VB-CABLE also installs.
        return result
            .OrderByDescending(c => c.CaptureOutput != null)
            .ThenByDescending(c => c.Vendor == "VB-CABLE")
            .ThenByDescending(c => c.RenderInput.Name.StartsWith("CABLE Input", StringComparison.OrdinalIgnoreCase))
            .ThenBy(c => c.RenderInput.Channels)
            .ToList();
    }

    private static AudioDeviceInfo? FindPair(AudioDeviceInfo render, List<AudioDeviceInfo> captures)
    {
        if (captures.Count == 0) return null;
        AudioDeviceInfo? best = null;
        int bestScore = int.MinValue;
        foreach (var c in captures)
        {
            int score = 0;
            if (!string.IsNullOrEmpty(render.AdapterName) && string.Equals(render.AdapterName, c.AdapterName, StringComparison.OrdinalIgnoreCase)) score += 10;
            // "CABLE-A Input" ↔ "CABLE-A Output", "Line 1 (…)" ↔ "Line 1 (…)"
            var rt = Token(render.Name);
            var ct = Token(c.Name);
            if (rt.Length > 0 && rt == ct) score += 20;
            if (VendorOf(render) == VendorOf(c)) score += 5;
            if (score > bestScore) { bestScore = score; best = c; }
        }
        return bestScore > 0 ? best : null;
    }

    /// <summary>Extracts the cable identity token, e.g. "CABLE-A" from "CABLE-A Input (VB-Audio Cable A)" or "Line 1" from "Line 1 (Virtual Audio Cable)".</summary>
    private static string Token(string name)
    {
        var n = name;
        int paren = n.IndexOf('(');
        if (paren > 0) n = n[..paren];
        n = n.Replace("Input", "", StringComparison.OrdinalIgnoreCase)
             .Replace("Output", "", StringComparison.OrdinalIgnoreCase)
             .Replace("Speakers", "", StringComparison.OrdinalIgnoreCase)
             .Replace("Microphone", "", StringComparison.OrdinalIgnoreCase);
        return n.Trim().ToUpperInvariant();
    }
}
