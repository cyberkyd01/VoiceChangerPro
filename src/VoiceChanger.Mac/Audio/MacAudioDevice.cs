namespace VoiceChanger.Mac.Audio;

/// <summary>Snapshot of a Core Audio device as seen through PortAudio.</summary>
public sealed record MacAudioDevice(int Index, string Name, int MaxInputChannels, int MaxOutputChannels, double DefaultSampleRate)
{
    public bool HasInput => MaxInputChannels > 0;
    public bool HasOutput => MaxOutputChannels > 0;

    /// <summary>A loopback cable: audio played into it comes out of its microphone side.</summary>
    public bool IsCable => DeviceClassifier.IsCable(Name);

    /// <summary>Not a real microphone or speaker (cables, app-specific virtual devices, other voice changers).</summary>
    public bool IsVirtual => DeviceClassifier.IsVirtual(Name);

    public override string ToString() => Name;
}

/// <summary>
/// Name-based classification of macOS audio devices. Only true loopback cables are used as the
/// destination for the processed voice; other virtual devices (Zoom, Teams, other voice changers)
/// are neither processed as microphones nor offered for monitoring.
/// </summary>
public static class DeviceClassifier
{
    private static readonly string[] Cables =
    {
        "BlackHole", "VB-Cable", "CABLE Input", "CABLE Output", "Loopback Audio", "Soundflower",
    };

    private static readonly string[] OtherVirtual =
    {
        "ZoomAudioDevice", "Microsoft Teams Audio", "Voxal", "Background Music", "WebexMediaAudioDevice",
        "Aggregate Device", "Multi-Output Device", "Descript Loopback", "Rogue Amoeba",
    };

    public const string BlackHoleUrl = "https://existential.audio/blackhole/";

    public static bool IsCable(string name) => Cables.Any(c => name.Contains(c, StringComparison.OrdinalIgnoreCase));

    public static bool IsVirtual(string name) => IsCable(name) || OtherVirtual.Any(c => name.Contains(c, StringComparison.OrdinalIgnoreCase));

    /// <summary>Lower is better: BlackHole 2ch first, then other cables.</summary>
    public static int CableRank(string name)
    {
        if (name.Contains("BlackHole 2ch", StringComparison.OrdinalIgnoreCase)) return 0;
        if (name.Contains("BlackHole", StringComparison.OrdinalIgnoreCase)) return 1;
        if (name.Contains("CABLE", StringComparison.OrdinalIgnoreCase)) return 2;
        return 3;
    }
}
