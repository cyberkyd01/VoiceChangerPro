namespace VoiceChanger.Core.Audio.Devices;

/// <summary>A virtual audio cable: the render endpoint we write into and the capture endpoint other apps read from.</summary>
public sealed record VirtualCable(AudioDeviceInfo RenderInput, AudioDeviceInfo? CaptureOutput, string Vendor)
{
    public string DisplayName => $"{Vendor}: {RenderInput.Name}" + (CaptureOutput != null ? $"  →  {CaptureOutput.Name}" : "  (no paired microphone endpoint found)");
}

/// <summary>
/// Detects installed virtual audio cable drivers (VB-CABLE, Virtual Audio Cable, VoiceMeeter) and pairs
/// their render and capture endpoints. Also recognises virtual endpoints that must never be used as a
/// cable or processed as a microphone (effect processors such as NVIDIA Broadcast or SteelSeries Sonar,
/// and loopback inputs such as Stereo Mix).
/// </summary>
public static class VirtualCableDetector
{
    /// <summary>
    /// True loopback cables: whatever is played into the render side comes out of the capture side.
    /// Only these are chosen automatically as the destination for the processed voice.
    /// </summary>
    private static readonly (string Match, string Vendor)[] CableVendors =
    {
        ("VB-Audio", "VB-CABLE"),
        ("CABLE Input", "VB-CABLE"),
        ("CABLE Output", "VB-CABLE"),
        ("VoiceMeeter", "VoiceMeeter"),
        ("Virtual Audio Cable", "Virtual Audio Cable"),
        ("Virtual Cable", "Virtual Cable"),
    };

    /// <summary>
    /// Virtual devices that are NOT cables. Their render side plays through the real headphones or
    /// speakers, so writing the processed voice there makes it come out of the user's earphones instead
    /// of reaching other apps. Their capture side is a virtual microphone fed from a physical one,
    /// so it is not processed a second time.
    /// </summary>
    private static readonly (string Match, string Vendor)[] ProcessorVendors =
    {
        ("NVIDIA Broadcast", "NVIDIA Broadcast"),
        ("Sonar", "SteelSeries Sonar"),
        ("Screaming Bee", "Screaming Bee"),
    };

    /// <summary>Capture endpoints that record what is playing on the speakers, never a voice.</summary>
    private static readonly string[] LoopbackCaptureNames =
    {
        "Stereo Mix", "Stereomix", "What U Hear", "Wave Out Mix", "Loopback",
    };

    public const string VbCableDownloadUrl = "https://vb-audio.com/Cable/";

    /// <summary>True for a loopback cable endpoint that can carry the processed voice to other apps.</summary>
    public static bool IsCable(AudioDeviceInfo d) => Matches(d, CableVendors) != null;

    /// <summary>True for an effect processor (NVIDIA Broadcast, SteelSeries Sonar…) whose render side plays through the real speakers.</summary>
    public static bool IsEffectProcessor(AudioDeviceInfo d) => !IsCable(d) && Matches(d, ProcessorVendors) != null;

    /// <summary>
    /// True for any endpoint that is not a physical microphone or a physical speaker: cables, effect
    /// processors and loopback inputs. Such capture endpoints are never processed, and such render
    /// endpoints are never offered for monitoring.
    /// </summary>
    public static bool LooksVirtual(AudioDeviceInfo d)
    {
        if (IsCable(d) || Matches(d, ProcessorVendors) != null) return true;
        if (d.Kind == AudioDeviceKind.Capture)
        {
            var text = d.Name + " " + d.AdapterName;
            foreach (var name in LoopbackCaptureNames)
                if (text.Contains(name, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    public static string VendorOf(AudioDeviceInfo d)
        => Matches(d, CableVendors) ?? Matches(d, ProcessorVendors) ?? "Virtual device";

    private static string? Matches(AudioDeviceInfo d, (string Match, string Vendor)[] table)
    {
        var text = d.Name + " " + d.AdapterName;
        foreach (var (match, vendor) in table)
            if (text.Contains(match, StringComparison.OrdinalIgnoreCase)) return vendor;
        return null;
    }

    /// <summary>Finds all virtual cables, pairing each virtual render endpoint with its most likely capture endpoint.</summary>
    public static IReadOnlyList<VirtualCable> Detect(IReadOnlyList<AudioDeviceInfo> renderDevices, IReadOnlyList<AudioDeviceInfo> captureDevices)
    {
        var result = new List<VirtualCable>();
        var virtualCaptures = captureDevices.Where(IsCable).ToList();
        foreach (var r in renderDevices.Where(IsCable))
        {
            var pair = FindPair(r, virtualCaptures);
            result.Add(new VirtualCable(r, pair, VendorOf(r)));
        }
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
