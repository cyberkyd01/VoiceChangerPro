using PortAudioSharp;
using VoiceChanger.Core.Dsp;
using VoiceChanger.Core.Logging;
using VoiceChanger.Core.Presets;
using PaStream = PortAudioSharp.Stream;

namespace VoiceChanger.Mac.Audio;

/// <summary>Owns the PortAudio library lifetime and lists devices.</summary>
public static class MacAudio
{
    private static bool _initialized;
    private static readonly object Sync = new();

    public static void EnsureInitialized()
    {
        lock (Sync)
        {
            if (_initialized) return;
            PortAudio.Initialize();
            _initialized = true;
            Log.Info("Audio", $"PortAudio {PortAudio.VersionInfo.versionText} initialised");
        }
    }

    /// <summary>Re-reads the device list. PortAudio only sees new devices after a restart, so this must not run while streams are open.</summary>
    public static void Restart()
    {
        lock (Sync)
        {
            if (_initialized) { try { PortAudio.Terminate(); } catch { /* ignore */ } _initialized = false; }
        }
        EnsureInitialized();
    }

    public static IReadOnlyList<MacAudioDevice> Devices()
    {
        EnsureInitialized();
        var list = new List<MacAudioDevice>();
        for (int i = 0; i < PortAudio.DeviceCount; i++)
        {
            var d = PortAudio.GetDeviceInfo(i);
            list.Add(new MacAudioDevice(i, d.name, d.maxInputChannels, d.maxOutputChannels, d.defaultSampleRate));
        }
        return list;
    }

    public static int DefaultInputIndex { get { EnsureInitialized(); return PortAudio.DefaultInputDevice; } }
    public static int DefaultOutputIndex { get { EnsureInitialized(); return PortAudio.DefaultOutputDevice; } }

    public static void Shutdown()
    {
        lock (Sync)
        {
            if (!_initialized) return;
            try { PortAudio.Terminate(); } catch { /* ignore */ }
            _initialized = false;
        }
    }
}

/// <summary>
/// Real-time chain for one microphone: PortAudio capture → <see cref="VoicePipeline"/> →
/// the virtual cable (what other apps hear) and, optionally, the headphones ("Hear myself").
/// </summary>
public sealed class MacVoiceEngine : IDisposable
{
    public const int FramesPerBuffer = 256;

    private readonly object _sync = new();
    private PaStream? _input;
    private volatile MacOutputSink? _cable;
    private volatile MacOutputSink? _monitor;
    private float[] _block = new float[8192];
    private int _inputChannels;
    private volatile float _voiceVolume = 1f;
    private float _voiceVolumeCurrent = 1f;
    private long _silentFrames;

    public VoicePipeline? Pipeline { get; private set; }
    public int SampleRate { get; private set; }
    public bool IsRunning => _input != null;
    public string? MicrophoneName { get; private set; }
    public string? CableName => _cable?.DeviceName;
    public string? MonitorName => _monitor?.DeviceName;
    public string? LastError { get; private set; }
    public long CallbackCount { get; private set; }
    /// <summary>Seconds of digital silence from the microphone (usually means microphone access is blocked).</summary>
    public double SilentSeconds => SampleRate > 0 ? _silentFrames / (double)SampleRate : 0;

    public float VoiceVolume { get => _voiceVolume; set => _voiceVolume = Math.Clamp(value, 0f, 2f); }

    /// <summary>Raised on a background thread when the microphone or an output stops unexpectedly.</summary>
    public event Action<string>? Faulted;

    public bool Start(MacAudioDevice mic, MacAudioDevice? cable, MacAudioDevice? monitor, float monitorVolume, VoiceProfile profile, GlobalProcessing global, bool bypass)
    {
        lock (_sync)
        {
            StopInternal();
            LastError = null;
            try
            {
                MacAudio.EnsureInitialized();
                SampleRate = (int)Math.Round(mic.DefaultSampleRate);
                if (SampleRate < 8000) SampleRate = 48000;
                Pipeline = new VoicePipeline(SampleRate) { Bypass = bypass };
                Pipeline.Apply(profile);
                Pipeline.ApplyGlobal(global);
                _silentFrames = 0;
                CallbackCount = 0;

                if (cable != null) _cable = OpenSink(cable, 1f);
                if (monitor != null) _monitor = OpenSink(monitor, monitorVolume);

                _inputChannels = 1;
                var p = new StreamParameters
                {
                    device = mic.Index,
                    channelCount = _inputChannels,
                    sampleFormat = SampleFormat.Float32,
                    suggestedLatency = PortAudio.GetDeviceInfo(mic.Index).defaultLowInputLatency,
                    hostApiSpecificStreamInfo = IntPtr.Zero,
                };
                var input = new PaStream(p, null, SampleRate, FramesPerBuffer, StreamFlags.ClipOff, OnInput, this);
                input.SetFinishedCallback(_ => { if (_input != null) Faulted?.Invoke("The microphone stopped (disconnected?)"); });
                input.Start();
                _input = input;
                MicrophoneName = mic.Name;
                Log.Info("Engine", $"Started '{mic.Name}' @ {SampleRate} Hz, DSP latency {Pipeline.LatencyMs:F1} ms, cable={cable?.Name ?? "none"}, monitor={monitor?.Name ?? "off"}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Engine", $"Failed to start '{mic.Name}'", ex);
                LastError = ex.Message;
                StopInternal();
                return false;
            }
        }
    }

    private MacOutputSink? OpenSink(MacAudioDevice device, float volume)
    {
        try
        {
            var sink = new MacOutputSink(device, SampleRate, FramesPerBuffer) { Volume = volume };
            sink.Failed += s => Faulted?.Invoke($"Output '{s.DeviceName}' stopped");
            return sink;
        }
        catch (Exception ex)
        {
            Log.Error("Engine", $"Could not open output '{device.Name}'", ex);
            LastError = $"Could not open '{device.Name}': {ex.Message}";
            return null;
        }
    }

    public void SetCable(MacAudioDevice? device)
    {
        lock (_sync)
        {
            if (!IsRunning || _cable?.DeviceName == device?.Name) return;
            var old = _cable;
            _cable = device != null ? OpenSink(device, 1f) : null;
            old?.Dispose();
        }
    }

    public void SetMonitor(MacAudioDevice? device, float volume)
    {
        lock (_sync)
        {
            if (!IsRunning) return;
            if (device != null && _monitor?.DeviceName == device.Name) { _monitor.Volume = volume; return; }
            var old = _monitor;
            _monitor = device != null ? OpenSink(device, volume) : null;
            old?.Dispose();
        }
    }

    public void SetMonitorVolume(float volume)
    {
        var m = _monitor;
        if (m != null) m.Volume = volume;
    }

    private unsafe StreamCallbackResult OnInput(IntPtr input, IntPtr output, uint frameCount, ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags flags, IntPtr user)
    {
        try
        {
            var pipeline = Pipeline;
            if (pipeline == null || input == IntPtr.Zero) return StreamCallbackResult.Continue;
            int frames = (int)frameCount;
            if (frames > _block.Length) return StreamCallbackResult.Continue; // never allocate on the audio thread
            var src = (float*)input;
            var block = _block.AsSpan(0, frames);
            for (int i = 0; i < frames; i++) block[i] = src[i * _inputChannels];

            pipeline.Process(block);
            CallbackCount++;
            if (pipeline.InputPeak > 3e-5f) _silentFrames = 0; else _silentFrames += frames;

            float target = _voiceVolume;
            if (target != 1f || Math.Abs(target - _voiceVolumeCurrent) > 1e-4f)
            {
                float start = _voiceVolumeCurrent, step = (target - start) / frames;
                for (int i = 0; i < frames; i++) block[i] *= start + step * i;
                _voiceVolumeCurrent = target;
            }

            _cable?.Write(block);
            _monitor?.Write(block);
        }
        catch (Exception ex)
        {
            Log.Error("Engine", "Audio callback error", ex);
        }
        return StreamCallbackResult.Continue;
    }

    public void Stop()
    {
        lock (_sync) StopInternal();
    }

    private void StopInternal()
    {
        var input = _input;
        _input = null;
        if (input != null)
        {
            try { input.Stop(); } catch { /* ignore */ }
            try { input.Dispose(); } catch { /* ignore */ }
            Log.Info("Engine", $"Stopped '{MicrophoneName}'");
        }
        var c = _cable; _cable = null; c?.Dispose();
        var m = _monitor; _monitor = null; m?.Dispose();
    }

    public void Dispose() => Stop();
}
