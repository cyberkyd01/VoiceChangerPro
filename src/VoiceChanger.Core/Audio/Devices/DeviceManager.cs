using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using VoiceChanger.Core.Logging;

namespace VoiceChanger.Core.Audio.Devices;

public enum AudioDeviceKind { Capture, Render }

/// <summary>Immutable snapshot of a WASAPI endpoint.</summary>
public sealed record AudioDeviceInfo(
    string Id,
    string Name,
    string AdapterName,
    AudioDeviceKind Kind,
    bool IsDefault,
    bool IsDefaultCommunications,
    int SampleRate,
    int Channels)
{
    public bool IsVirtual => VirtualCableDetector.LooksVirtual(this);
    public override string ToString() => Name;
}

public sealed class DeviceChangedEventArgs : EventArgs
{
    public DeviceChangedEventArgs(string deviceId, string reason) { DeviceId = deviceId; Reason = reason; }
    public string DeviceId { get; }
    public string Reason { get; }
}

/// <summary>
/// Enumerates capture and render endpoints and raises events for hot-plug, removal,
/// state changes and default-device changes via IMMNotificationClient.
/// </summary>
public sealed class DeviceManager : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly NotificationClient _client;
    private bool _disposed;

    /// <summary>Raised (on a COM thread) whenever any device is added, removed, changes state or default status.</summary>
    public event EventHandler<DeviceChangedEventArgs>? DevicesChanged;

    public DeviceManager()
    {
        _client = new NotificationClient(this);
        try
        {
            _enumerator.RegisterEndpointNotificationCallback(_client);
        }
        catch (Exception ex)
        {
            Log.Error("Devices", "Failed to register endpoint notification callback; hot-plug detection disabled", ex);
        }
    }

    public IReadOnlyList<AudioDeviceInfo> GetCaptureDevices() => Enumerate(DataFlow.Capture);
    public IReadOnlyList<AudioDeviceInfo> GetRenderDevices() => Enumerate(DataFlow.Render);

    public AudioDeviceInfo? GetDevice(string id)
    {
        try
        {
            using var d = _enumerator.GetDevice(id);
            if (d == null || d.State != DeviceState.Active) return null;
            var flow = d.DataFlow;
            var (def, com) = GetDefaults(flow);
            return ToInfo(d, def, com);
        }
        catch { return null; }
    }

    /// <summary>Returns the underlying MMDevice (caller must dispose).</summary>
    public MMDevice? GetMMDevice(string id)
    {
        try { return _enumerator.GetDevice(id); }
        catch (Exception ex)
        {
            Log.Warn("Devices", $"GetDevice({id}) failed: {ex.Message}");
            return null;
        }
    }

    public MMDevice? GetDefaultRenderDevice(Role role = Role.Multimedia)
    {
        try
        {
            if (!_enumerator.HasDefaultAudioEndpoint(DataFlow.Render, role)) return null;
            return _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, role);
        }
        catch { return null; }
    }

    public string? GetDefaultDeviceId(DataFlow flow, Role role)
    {
        try
        {
            if (!_enumerator.HasDefaultAudioEndpoint(flow, role)) return null;
            using var d = _enumerator.GetDefaultAudioEndpoint(flow, role);
            return d.ID;
        }
        catch { return null; }
    }

    private (string? def, string? com) GetDefaults(DataFlow flow)
        => (GetDefaultDeviceId(flow, Role.Multimedia), GetDefaultDeviceId(flow, Role.Communications));

    private IReadOnlyList<AudioDeviceInfo> Enumerate(DataFlow flow)
    {
        var list = new List<AudioDeviceInfo>();
        try
        {
            var (def, com) = GetDefaults(flow);
            foreach (var d in _enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
            {
                try { list.Add(ToInfo(d, def, com)); }
                catch (Exception ex) { Log.Warn("Devices", $"Skipping device: {ex.Message}"); }
                finally { d.Dispose(); }
            }
        }
        catch (Exception ex)
        {
            Log.Error("Devices", $"Enumeration of {flow} devices failed", ex);
        }
        return list.OrderByDescending(x => x.IsDefault).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static AudioDeviceInfo ToInfo(MMDevice d, string? defaultId, string? comId)
    {
        int rate = 0, ch = 0;
        try
        {
            var fmt = d.AudioClient.MixFormat;
            rate = fmt.SampleRate;
            ch = fmt.Channels;
        }
        catch { /* some endpoints refuse to open */ }
        string adapter;
        try { adapter = d.DeviceFriendlyName; } catch { adapter = string.Empty; }
        return new AudioDeviceInfo(
            d.ID,
            d.FriendlyName,
            adapter,
            d.DataFlow == DataFlow.Capture ? AudioDeviceKind.Capture : AudioDeviceKind.Render,
            d.ID == defaultId,
            d.ID == comId,
            rate,
            ch);
    }

    private void Raise(string id, string reason)
    {
        if (_disposed) return;
        Log.Debug("Devices", $"{reason}: {id}");
        try { DevicesChanged?.Invoke(this, new DeviceChangedEventArgs(id, reason)); }
        catch (Exception ex) { Log.Error("Devices", "DevicesChanged handler threw", ex); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _enumerator.UnregisterEndpointNotificationCallback(_client); } catch { /* ignore */ }
        _enumerator.Dispose();
    }

    private sealed class NotificationClient : IMMNotificationClient
    {
        private readonly DeviceManager _owner;
        public NotificationClient(DeviceManager owner) => _owner = owner;

        public void OnDeviceStateChanged(string deviceId, DeviceState newState) => _owner.Raise(deviceId, $"StateChanged({newState})");
        public void OnDeviceAdded(string pwstrDeviceId) => _owner.Raise(pwstrDeviceId, "Added");
        public void OnDeviceRemoved(string deviceId) => _owner.Raise(deviceId, "Removed");
        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            // Only one notification per flow is needed by listeners; Multimedia is the one users see in Settings.
            if (role == Role.Multimedia || role == Role.Communications)
                _owner.Raise(defaultDeviceId ?? string.Empty, $"DefaultChanged({flow},{role})");
        }
        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { /* noisy; ignore */ }
    }
}
