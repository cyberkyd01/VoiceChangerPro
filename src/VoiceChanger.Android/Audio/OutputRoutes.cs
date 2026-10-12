using Android.Media;
using VoiceChangerPro.AndroidApp.Diagnostics;

namespace VoiceChangerPro.AndroidApp.Audio;

public enum HeadphoneKind { None, Wired, Usb, Bluetooth }

public readonly record struct HeadphoneStatus(HeadphoneKind Kind, string Name)
{
    public bool Connected => Kind != HeadphoneKind.None;
}

/// <summary>
/// Finds out whether the sound will go to headphones or to the loudspeaker. Live mode through the loudspeaker
/// feeds the changed voice straight back into the microphone, which howls, so the UI warns about it.
/// </summary>
public static class OutputRoutes
{
    public static HeadphoneStatus Detect(AudioManager? audioManager)
    {
        if (audioManager == null) return new(HeadphoneKind.None, "");
        AudioDeviceInfo[]? devices;
        try { devices = audioManager.GetDevices(GetDevicesTargets.Outputs); }
        catch (Exception ex)
        {
            AppLog.Warn("Routes", $"Could not list output devices: {ex.Message}");
            return new(HeadphoneKind.None, "");
        }

        HeadphoneStatus best = new(HeadphoneKind.None, "");
        foreach (var d in devices ?? Array.Empty<AudioDeviceInfo>())
        {
            var kind = Classify(d.Type);
            if (kind == HeadphoneKind.None) continue;
            // Prefer wired over USB over Bluetooth: Android routes media the same way.
            if (best.Kind == HeadphoneKind.None || Rank(kind) < Rank(best.Kind))
                best = new(kind, d.ProductNameFormatted?.ToString() ?? "");
        }
        return best;
    }

    private static int Rank(HeadphoneKind k) => k switch
    {
        HeadphoneKind.Wired => 0,
        HeadphoneKind.Usb => 1,
        HeadphoneKind.Bluetooth => 2,
        _ => 3
    };

    private static HeadphoneKind Classify(AudioDeviceType type) => type switch
    {
        AudioDeviceType.WiredHeadphones or AudioDeviceType.WiredHeadset => HeadphoneKind.Wired,
        AudioDeviceType.UsbHeadset or AudioDeviceType.UsbDevice or AudioDeviceType.UsbAccessory => HeadphoneKind.Usb,
        AudioDeviceType.BluetoothA2dp or AudioDeviceType.BleHeadset or AudioDeviceType.HearingAid => HeadphoneKind.Bluetooth,
        _ => HeadphoneKind.None
    };
}
