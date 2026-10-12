using PortAudioSharp;
using VoiceChanger.Core.Logging;
using PaStream = PortAudioSharp.Stream;

namespace VoiceChanger.Mac.Audio;

/// <summary>
/// Plays processed mono audio on one output device (the virtual cable or the headphones).
/// The microphone and the output run on different hardware clocks, so audio passes through a
/// lock-free ring buffer that is kept near a small target fill: a growing backlog is trimmed by
/// dropping one sample in 96 (inaudible for speech) and an empty buffer plays silence.
/// </summary>
public sealed class MacOutputSink : IDisposable
{
    private readonly float[] _ring;
    private int _read, _write; // single producer (mic callback) / single consumer (output callback)
    private readonly int _targetFill;
    private readonly int _channels;
    private readonly PaStream _stream;
    private int _dropPhase;
    private volatile bool _disposed;

    public string DeviceName { get; }
    public float Volume { get; set; } = 1f;
    public int Underruns { get; private set; }
    public int DriftCorrections { get; private set; }
    public event Action<MacOutputSink>? Failed;

    public MacOutputSink(MacAudioDevice device, int sampleRate, int framesPerBuffer)
    {
        DeviceName = device.Name;
        _channels = Math.Clamp(device.MaxOutputChannels, 1, 2);
        _targetFill = framesPerBuffer * 4;
        _ring = new float[sampleRate * 2];

        var p = new StreamParameters
        {
            device = device.Index,
            channelCount = _channels,
            sampleFormat = SampleFormat.Float32,
            suggestedLatency = PortAudio.GetDeviceInfo(device.Index).defaultLowOutputLatency,
            hostApiSpecificStreamInfo = IntPtr.Zero,
        };
        Prefill();
        _stream = new PaStream(null, p, sampleRate, (uint)framesPerBuffer, StreamFlags.ClipOff, OnOutput, this);
        _stream.SetFinishedCallback(_ => { if (!_disposed) Failed?.Invoke(this); });
        _stream.Start();
        Log.Info("Output", $"Sink started: {DeviceName} ({_channels} ch @ {sampleRate} Hz)");
    }

    private int Count => (Volatile.Read(ref _write) - Volatile.Read(ref _read) + _ring.Length) % _ring.Length;

    private void Prefill()
    {
        for (int i = 0; i < _targetFill; i++) Push(0f);
    }

    private void Push(float v)
    {
        int w = _write;
        int next = (w + 1) % _ring.Length;
        if (next == Volatile.Read(ref _read)) return; // full: drop (only happens if the output stalled)
        _ring[w] = v;
        Volatile.Write(ref _write, next);
    }

    /// <summary>Called from the microphone callback with each processed block.</summary>
    public void Write(ReadOnlySpan<float> mono)
    {
        if (_disposed) return;
        int fill = Count;
        if (fill > _targetFill * 8)
        {
            // output stalled for a while: jump back to the target latency
            Volatile.Write(ref _read, (_write - _targetFill + _ring.Length) % _ring.Length);
            DriftCorrections++;
        }
        else if (fill > _targetFill + _targetFill / 2)
        {
            // output clock slightly slower than the microphone clock: thin by ~1 % until back on target
            for (int i = 0; i < mono.Length; i++)
            {
                if (++_dropPhase >= 96) { _dropPhase = 0; continue; }
                Push(mono[i]);
            }
            DriftCorrections++;
            return;
        }
        for (int i = 0; i < mono.Length; i++) Push(mono[i]);
    }

    private unsafe StreamCallbackResult OnOutput(IntPtr input, IntPtr output, uint frameCount, ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags flags, IntPtr user)
    {
        var dst = (float*)output;
        float vol = Volume;
        int r = _read;
        int available = (Volatile.Read(ref _write) - r + _ring.Length) % _ring.Length;
        int n = (int)Math.Min(frameCount, (uint)available);
        int o = 0;
        for (int i = 0; i < n; i++)
        {
            float s = _ring[r] * vol;
            r = (r + 1) % _ring.Length;
            for (int c = 0; c < _channels; c++) dst[o++] = s;
        }
        Volatile.Write(ref _read, r);
        for (int i = n; i < frameCount; i++)
            for (int c = 0; c < _channels; c++) dst[o++] = 0f;
        if (n < frameCount && n == 0) Underruns++;
        return StreamCallbackResult.Continue;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _stream.Stop(); } catch { /* ignore */ }
        try { _stream.Dispose(); } catch { /* ignore */ }
    }
}
