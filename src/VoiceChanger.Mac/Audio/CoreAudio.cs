using System.Runtime.InteropServices;
using System.Text;
using VoiceChanger.Core.Logging;

namespace VoiceChanger.Mac.Audio;

/// <summary>
/// Minimal Core Audio access for what PortAudio cannot do: reading and changing the system default
/// input device (so every app hears the processed voice) and detecting device-list changes.
/// </summary>
public static unsafe class CoreAudio
{
    private const string CoreAudioLib = "/System/Library/Frameworks/CoreAudio.framework/CoreAudio";
    private const string CoreFoundationLib = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    private const uint SystemObject = 1;
    private const uint ScopeGlobal = 0x676C6F62;  // 'glob'
    private const uint ScopeInput = 0x696E7074;   // 'inpt'
    private const uint ElementMain = 0;
    private const uint PropDevices = 0x64657623;  // 'dev#'
    private const uint PropDefaultInput = 0x64496E20;  // 'dIn '
    private const uint PropDefaultOutput = 0x644F7574; // 'dOut'
    private const uint PropName = 0x6C6E616D;     // 'lnam'
    private const uint PropStreams = 0x73746D23;  // 'stm#'
    private const uint CFStringEncodingUTF8 = 0x08000100;

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyAddress
    {
        public uint Selector, Scope, Element;
        public PropertyAddress(uint selector, uint scope = ScopeGlobal) { Selector = selector; Scope = scope; Element = ElementMain; }
    }

    [DllImport(CoreAudioLib)]
    private static extern int AudioObjectGetPropertyDataSize(uint id, ref PropertyAddress address, uint qualifierSize, IntPtr qualifier, out uint size);

    [DllImport(CoreAudioLib)]
    private static extern int AudioObjectGetPropertyData(uint id, ref PropertyAddress address, uint qualifierSize, IntPtr qualifier, ref uint size, void* data);

    [DllImport(CoreAudioLib)]
    private static extern int AudioObjectSetPropertyData(uint id, ref PropertyAddress address, uint qualifierSize, IntPtr qualifier, uint size, void* data);

    [DllImport(CoreFoundationLib)]
    private static extern long CFStringGetLength(IntPtr str);

    [DllImport(CoreFoundationLib)]
    private static extern bool CFStringGetCString(IntPtr str, byte* buffer, long bufferSize, uint encoding);

    [DllImport(CoreFoundationLib)]
    private static extern void CFRelease(IntPtr obj);

    private static uint[] DeviceIds()
    {
        var addr = new PropertyAddress(PropDevices);
        if (AudioObjectGetPropertyDataSize(SystemObject, ref addr, 0, IntPtr.Zero, out uint size) != 0 || size == 0) return Array.Empty<uint>();
        var ids = new uint[size / sizeof(uint)];
        fixed (uint* p = ids)
            if (AudioObjectGetPropertyData(SystemObject, ref addr, 0, IntPtr.Zero, ref size, p) != 0) return Array.Empty<uint>();
        return ids;
    }

    private static string? DeviceName(uint id)
    {
        var addr = new PropertyAddress(PropName);
        IntPtr cf = IntPtr.Zero;
        uint size = (uint)IntPtr.Size;
        if (AudioObjectGetPropertyData(id, ref addr, 0, IntPtr.Zero, ref size, &cf) != 0 || cf == IntPtr.Zero) return null;
        try
        {
            long len = CFStringGetLength(cf);
            int cap = (int)(len * 4 + 1);
            var buf = new byte[cap];
            fixed (byte* b = buf)
                if (!CFStringGetCString(cf, b, cap, CFStringEncodingUTF8)) return null;
            int n = Array.IndexOf(buf, (byte)0);
            return Encoding.UTF8.GetString(buf, 0, n < 0 ? buf.Length : n);
        }
        finally { CFRelease(cf); }
    }

    private static bool HasInputStreams(uint id)
    {
        var addr = new PropertyAddress(PropStreams, ScopeInput);
        return AudioObjectGetPropertyDataSize(id, ref addr, 0, IntPtr.Zero, out uint size) == 0 && size > 0;
    }

    private static uint GetDefault(uint selector)
    {
        var addr = new PropertyAddress(selector);
        uint id = 0, size = sizeof(uint);
        return AudioObjectGetPropertyData(SystemObject, ref addr, 0, IntPtr.Zero, ref size, &id) == 0 ? id : 0;
    }

    /// <summary>Name of the current system default microphone.</summary>
    public static string? DefaultInputName
    {
        get { try { var id = GetDefault(PropDefaultInput); return id == 0 ? null : DeviceName(id); } catch { return null; } }
    }

    /// <summary>Name of the current system default speakers/headphones.</summary>
    public static string? DefaultOutputName
    {
        get { try { var id = GetDefault(PropDefaultOutput); return id == 0 ? null : DeviceName(id); } catch { return null; } }
    }

    /// <summary>Makes the named input device the system default microphone. Returns false if it was not found or refused.</summary>
    public static bool SetDefaultInput(string name)
    {
        try
        {
            foreach (var id in DeviceIds())
            {
                if (!HasInputStreams(id) || DeviceName(id) != name) continue;
                var addr = new PropertyAddress(PropDefaultInput);
                uint value = id;
                int err = AudioObjectSetPropertyData(SystemObject, ref addr, 0, IntPtr.Zero, sizeof(uint), &value);
                if (err == 0) { Log.Info("CoreAudio", $"Default input set to '{name}'"); return true; }
                Log.Warn("CoreAudio", $"Setting default input to '{name}' failed (OSStatus {err})");
                return false;
            }
        }
        catch (Exception ex) { Log.Error("CoreAudio", "SetDefaultInput failed", ex); }
        return false;
    }

    /// <summary>A cheap fingerprint of the device list, used to notice devices being plugged in or removed.</summary>
    public static string DeviceListFingerprint()
    {
        try { return string.Join("|", DeviceIds()); }
        catch { return string.Empty; }
    }
}
