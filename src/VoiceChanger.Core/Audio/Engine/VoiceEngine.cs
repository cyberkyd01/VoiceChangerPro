using System.ComponentModel;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using VoiceChanger.Core.Audio.Devices;
using VoiceChanger.Core.Dsp;
using VoiceChanger.Core.Logging;
using VoiceChanger.Core.Presets;
using VoiceChanger.Core.Settings;

namespace VoiceChanger.Core.Audio.Engine;

public enum EngineState { Stopped, Starting, Running, Faulted }

/// <summary>
/// One real-time processing instance per physical microphone: WASAPI capture → mono float →
/// <see cref="VoicePipeline"/> → one or more <see cref="OutputSink"/>s (virtual cable + monitoring).
/// Also supports recording a short raw test clip and looping it through the chain for hands-free tuning.
/// </summary>
public sealed class VoiceEngine : IDisposable
{
    private static readonly Guid SubtypeIeeeFloat = new("00000003-0000-0010-8000-00aa00389b71");

    private readonly DeviceManager _devices;
    private readonly object _sync = new();
    private WasapiCapture? _capture;
    private OutputSink? _virtualSink;
    private OutputSink? _monitorSink;
    private float[] _mono = new float[4096];
    private bool _disposed;
    private int _bufferMs;

    // test clip
    private float[]? _clip;
    private int _clipLen, _clipPos, _clipRecordRemaining;
    private volatile bool _loopClip;

    public MicrophoneConfig Config { get; }
    public string DeviceId => Config.DeviceId;
    public string DeviceName { get; private set; }
    public VoiceProfile Profile => Config.Profile;
    public VoicePipeline? Pipeline { get; private set; }
    public EngineState State { get; private set; } = EngineState.Stopped;
    public string? LastError { get; private set; }
    public int SampleRate { get; private set; }
    public int InputChannels { get; private set; }
    public bool IsPresent { get; set; } = true;

    public string? VirtualOutputId => _virtualSink?.DeviceId;
    public string? VirtualOutputName => _virtualSink?.Name;
    public string? MonitorOutputId => _monitorSink?.DeviceId;
    public bool HasClip => _clipLen > 0;
    public bool IsRecordingClip => _clipRecordRemaining > 0;
    public double ClipSeconds => SampleRate > 0 ? _clipLen / (double)SampleRate : 0;
    public bool LoopClip { get => _loopClip; set { _loopClip = value; _clipPos = 0; } }

    private bool _master = true;
    /// <summary>Global master switch (owned by the manager).</summary>
    public bool MasterEnabled { get => _master; set { _master = value; UpdateBypass(); } }

    /// <summary>Optional background audio bed mixed into this microphone's output (owned by the manager).</summary>
    public BackgroundTrack? Background { get; set; }

    private volatile float _voiceVolume = 1f;
    private float _voiceVolumeCurrent = 1f;
    /// <summary>Global voice level on the microphone channel (0..2, 1 = unity), independent of presets.</summary>
    public float VoiceVolume { get => _voiceVolume; set => _voiceVolume = Math.Clamp(value, 0f, 2f); }

    private GlobalProcessing _global = GlobalProcessing.Off;
    /// <summary>Preset-independent clean-up settings (noise suppression, voice isolation); applied live.</summary>
    public GlobalProcessing Global
    {
        get => _global;
        set { _global = value; Pipeline?.ApplyGlobal(value); }
    }

    /// <summary>Per-microphone effect switch. Off = transparent pass-through (audio still routed).</summary>
    public bool EffectEnabled
    {
        get => Config.EffectEnabled;
        set { Config.EffectEnabled = value; UpdateBypass(); StateChanged?.Invoke(this); }
    }

    /// <summary>Estimated end-to-end latency in milliseconds (capture buffer + DSP + output buffer).</summary>
    public double EstimatedLatencyMs => Pipeline == null ? 0 : _bufferMs + Pipeline.LatencyMs + _bufferMs * 2 + 10;

    public event Action<VoiceEngine>? StateChanged;
    public event Action<VoiceEngine>? ClipRecorded;

    public VoiceEngine(DeviceManager devices, MicrophoneConfig config, int bufferMs)
    {
        _devices = devices;
        Config = config;
        DeviceName = config.DeviceName;
        _bufferMs = Math.Clamp(bufferMs, 5, 100);
        Profile.PropertyChanged += OnProfileChanged;
    }

    private void OnProfileChanged(object? sender, PropertyChangedEventArgs e)
    {
        Pipeline?.Apply(Profile);
    }

    private void UpdateBypass()
    {
        if (Pipeline != null) Pipeline.Bypass = !(_master && Config.EffectEnabled);
    }

    /// <summary>Starts capture and routing. Safe to call when already running (no-op).</summary>
    public bool Start(string? virtualOutputId, string? monitorOutputId, bool monitorEnabled)
    {
        lock (_sync)
        {
            if (_disposed) return false;
            if (State == EngineState.Running) return true;
            State = EngineState.Starting;
            LastError = null;
            try
            {
                var dev = _devices.GetMMDevice(DeviceId) ?? throw new InvalidOperationException("Microphone is not connected");
                if (dev.State != DeviceState.Active) throw new InvalidOperationException($"Microphone state is {dev.State}");
                DeviceName = dev.FriendlyName;
                Config.DeviceName = DeviceName;

                _capture = new WasapiCapture(dev, true, _bufferMs);
                var fmt = _capture.WaveFormat;
                SampleRate = fmt.SampleRate;
                InputChannels = fmt.Channels;
                Pipeline = new VoicePipeline(SampleRate);
                Pipeline.Apply(Profile);
                Pipeline.ApplyGlobal(_global);
                UpdateBypass();

                _capture.DataAvailable += OnDataAvailable;
                _capture.RecordingStopped += OnRecordingStopped;

                if (!string.IsNullOrEmpty(virtualOutputId)) TryCreateVirtualSink(virtualOutputId);
                if (monitorEnabled && !string.IsNullOrEmpty(monitorOutputId)) TryCreateMonitorSink(monitorOutputId);

                _capture.StartRecording();
                State = EngineState.Running;
                Log.Info("Engine", $"Started '{DeviceName}' @ {SampleRate} Hz / {InputChannels} ch, {fmt.BitsPerSample}-bit {fmt.Encoding}, DSP latency {Pipeline.LatencyMs:F1} ms");
            }
            catch (Exception ex)
            {
                Log.Error("Engine", $"Failed to start '{DeviceName}'", ex);
                LastError = ex.Message;
                State = EngineState.Faulted;
                CleanupCapture();
            }
        }
        StateChanged?.Invoke(this);
        return State == EngineState.Running;
    }

    public void Stop()
    {
        lock (_sync)
        {
            if (State == EngineState.Stopped) return;
            CleanupCapture();
            State = EngineState.Stopped;
            Log.Info("Engine", $"Stopped '{DeviceName}'");
        }
        StateChanged?.Invoke(this);
    }

    private void CleanupCapture()
    {
        var cap = _capture;
        _capture = null;
        if (cap != null)
        {
            try { cap.DataAvailable -= OnDataAvailable; cap.RecordingStopped -= OnRecordingStopped; } catch { }
            try { cap.StopRecording(); } catch { }
            try { cap.Dispose(); } catch { }
        }
        _virtualSink?.Dispose(); _virtualSink = null;
        _monitorSink?.Dispose(); _monitorSink = null;
    }

    private void TryCreateVirtualSink(string id)
    {
        try
        {
            _virtualSink?.Dispose();
            _virtualSink = new OutputSink(_devices, id, SampleRate, _bufferMs);
            _virtualSink.Stopped += OnSinkStopped;
        }
        catch (Exception ex)
        {
            Log.Error("Engine", $"Could not open virtual output {id}", ex);
            LastError = "Virtual cable output unavailable: " + ex.Message;
            _virtualSink = null;
        }
    }

    private void TryCreateMonitorSink(string id)
    {
        try
        {
            _monitorSink?.Dispose();
            _monitorSink = new OutputSink(_devices, id, SampleRate, _bufferMs) { Volume = Config.MonitorVolume };
            _monitorSink.Stopped += OnSinkStopped;
        }
        catch (Exception ex)
        {
            Log.Error("Engine", $"Could not open monitor output {id}", ex);
            LastError = "Monitor output unavailable: " + ex.Message;
            _monitorSink = null;
        }
    }

    private void OnSinkStopped(OutputSink sink, Exception? ex)
    {
        // A render device vanished (e.g. headphones unplugged). Drop the sink; the manager may re-route later.
        lock (_sync)
        {
            if (ReferenceEquals(sink, _virtualSink)) { _virtualSink = null; LastError = "Virtual cable output stopped"; }
            if (ReferenceEquals(sink, _monitorSink)) { _monitorSink = null; }
            sink.Dispose();
        }
        StateChanged?.Invoke(this);
    }

    /// <summary>Changes the virtual-cable output without interrupting capture.</summary>
    public void SetVirtualOutput(string? deviceId)
    {
        lock (_sync)
        {
            if (State != EngineState.Running) return;
            if (_virtualSink?.DeviceId == deviceId) return;
            _virtualSink?.Dispose(); _virtualSink = null;
            if (!string.IsNullOrEmpty(deviceId)) TryCreateVirtualSink(deviceId);
        }
        StateChanged?.Invoke(this);
    }

    /// <summary>Enables or disables live monitoring on the given output device.</summary>
    public void SetMonitor(string? deviceId, bool enabled)
    {
        lock (_sync)
        {
            Config.MonitorEnabled = enabled;
            if (State != EngineState.Running) return;
            if (!enabled || string.IsNullOrEmpty(deviceId))
            {
                _monitorSink?.Dispose(); _monitorSink = null;
            }
            else if (_monitorSink?.DeviceId != deviceId)
            {
                _monitorSink?.Dispose(); _monitorSink = null;
                TryCreateMonitorSink(deviceId);
            }
        }
        StateChanged?.Invoke(this);
    }

    public void SetMonitorVolume(float volume)
    {
        Config.MonitorVolume = Math.Clamp(volume, 0f, 1.5f);
        var s = _monitorSink;
        if (s != null) s.Volume = Config.MonitorVolume;
    }

    public bool MonitorActive => _monitorSink != null;
    public bool VirtualActive => _virtualSink != null;

    /// <summary>Starts recording <paramref name="seconds"/> of raw microphone input as a test clip.</summary>
    public void RecordClip(double seconds)
    {
        if (SampleRate <= 0) return;
        int n = (int)(seconds * SampleRate);
        _clip = new float[n];
        _clipLen = 0;
        _clipPos = 0;
        _clipRecordRemaining = n;
        StateChanged?.Invoke(this);
    }

    public void ClearClip()
    {
        _loopClip = false;
        _clip = null; _clipLen = 0; _clipPos = 0; _clipRecordRemaining = 0;
        StateChanged?.Invoke(this);
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var cap = _capture;
        var pipeline = Pipeline;
        if (cap == null || pipeline == null || e.BytesRecorded == 0) return;
        try
        {
            int frames = ConvertToMono(cap.WaveFormat, e.Buffer, e.BytesRecorded);
            var block = _mono.AsSpan(0, frames);

            // test clip recording (raw input, before processing)
            if (_clipRecordRemaining > 0 && _clip != null)
            {
                int take = Math.Min(_clipRecordRemaining, frames);
                block.Slice(0, take).CopyTo(_clip.AsSpan(_clipLen));
                _clipLen += take;
                _clipRecordRemaining -= take;
                if (_clipRecordRemaining == 0)
                {
                    Log.Info("Engine", $"Test clip recorded ({ClipSeconds:F1} s)");
                    ClipRecorded?.Invoke(this);
                }
            }

            // clip playback replaces the live input while looping
            if (_loopClip && _clipLen > 0 && _clip != null)
            {
                for (int i = 0; i < frames; i++)
                {
                    block[i] = _clip[_clipPos];
                    if (++_clipPos >= _clipLen) _clipPos = 0;
                }
            }

            pipeline.Process(block);
            TrackSilence(pipeline.InputPeak, frames);

            // global voice level (outside presets), ramped across the block to avoid clicks
            float target = _voiceVolume;
            if (Math.Abs(target - _voiceVolumeCurrent) > 1e-4f || target != 1f)
            {
                float start = _voiceVolumeCurrent, step = (target - start) / Math.Max(1, frames);
                for (int i = 0; i < frames; i++) block[i] *= start + step * i;
                _voiceVolumeCurrent = target;
            }

            // background audio bed is mixed AFTER the voice chain so effects never alter it
            Background?.MixInto(block);

            _virtualSink?.Write(block);
            _monitorSink?.Write(block);
        }
        catch (Exception ex)
        {
            Log.Error("Engine", "Audio callback error", ex);
        }
    }

    private long _silentSamples;
    private bool _everHadSignal;

    /// <summary>Seconds of continuous digital silence (raw input peak below -90 dBFS) since the last real signal.</summary>
    public double InputSilentSeconds => SampleRate > 0 ? _silentSamples / (double)SampleRate : 0;
    /// <summary>True once any non-silent audio has been received since start.</summary>
    public bool EverHadSignal => _everHadSignal;

    private void TrackSilence(float inputPeak, int frames)
    {
        if (_loopClip && _clipLen > 0) return; // clip playback is not live input
        if (inputPeak > 3e-5f) { _silentSamples = 0; _everHadSignal = true; }
        else _silentSamples += frames;
    }

    private int ConvertToMono(WaveFormat fmt, byte[] buffer, int bytes)
    {
        int channels = fmt.Channels;
        int bytesPerSample = fmt.BitsPerSample / 8;
        int frames = bytes / (bytesPerSample * channels);
        if (_mono.Length < frames) _mono = new float[frames * 2];

        bool isFloat = fmt.Encoding == WaveFormatEncoding.IeeeFloat
                       || (fmt is WaveFormatExtensible ext && ext.SubFormat == SubtypeIeeeFloat)
                       || (fmt.Encoding == WaveFormatEncoding.Extensible && fmt.BitsPerSample == 32 && IsFloatExtensible(fmt));
        float inv = 1f / channels;
        var span = buffer.AsSpan(0, bytes);

        if (isFloat && bytesPerSample == 4)
        {
            var f = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(span);
            for (int i = 0; i < frames; i++)
            {
                float s = 0;
                int b = i * channels;
                for (int c = 0; c < channels; c++) s += f[b + c];
                _mono[i] = s * inv;
            }
        }
        else if (bytesPerSample == 2)
        {
            var s16 = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, short>(span);
            for (int i = 0; i < frames; i++)
            {
                float s = 0;
                int b = i * channels;
                for (int c = 0; c < channels; c++) s += s16[b + c];
                _mono[i] = s * inv / 32768f;
            }
        }
        else if (bytesPerSample == 3)
        {
            for (int i = 0; i < frames; i++)
            {
                float s = 0;
                for (int c = 0; c < channels; c++)
                {
                    int o = (i * channels + c) * 3;
                    int v = (span[o] << 8) | (span[o + 1] << 16) | (span[o + 2] << 24);
                    s += v / 2147483648f;
                }
                _mono[i] = s * inv;
            }
        }
        else if (bytesPerSample == 4)
        {
            var s32 = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, int>(span);
            for (int i = 0; i < frames; i++)
            {
                float s = 0;
                int b = i * channels;
                for (int c = 0; c < channels; c++) s += s32[b + c] / 2147483648f;
                _mono[i] = s * inv;
            }
        }
        else
        {
            Array.Clear(_mono, 0, frames);
        }
        return frames;
    }

    private static bool IsFloatExtensible(WaveFormat fmt)
    {
        // WaveFormatExtensible SubFormat GUID lives at byte offset 24 of the WAVEFORMATEXTENSIBLE struct
        try
        {
            var bytes = new byte[fmt.ExtraSize + 18];
            using var ms = new MemoryStream(bytes);
            using var bw = new BinaryWriter(ms);
            fmt.Serialize(bw);
            if (bytes.Length < 40) return false;
            var guid = new Guid(bytes.AsSpan(24, 16));
            return guid == SubtypeIeeeFloat;
        }
        catch { return false; }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (_disposed) return;
        bool unexpected;
        lock (_sync)
        {
            unexpected = State == EngineState.Running;
            if (unexpected)
            {
                LastError = e.Exception?.Message ?? "Microphone stopped (disconnected?)";
                Log.Warn("Engine", $"Capture stopped unexpectedly on '{DeviceName}': {LastError}");
                CleanupCapture();
                State = EngineState.Faulted;
            }
        }
        if (unexpected) StateChanged?.Invoke(this);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Profile.PropertyChanged -= OnProfileChanged;
        lock (_sync) CleanupCapture();
        State = EngineState.Stopped;
    }
}
