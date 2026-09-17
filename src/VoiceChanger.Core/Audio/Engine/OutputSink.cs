using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using VoiceChanger.Core.Audio.Devices;
using VoiceChanger.Core.Logging;

namespace VoiceChanger.Core.Audio.Engine;

/// <summary>
/// Pushes processed mono audio into a WASAPI render endpoint (virtual cable input or headphones),
/// handling sample-rate and channel conversion and slow clock drift between capture and render devices.
/// </summary>
public sealed class OutputSink : IDisposable
{
    private readonly MonoRing _ring;
    private readonly WasapiOut _out;
    private readonly MMDevice _device;
    private readonly int _targetFill, _highWater;
    private volatile bool _disposed;

    public string DeviceId { get; }
    public string Name { get; }
    public int DeviceSampleRate { get; }
    public int DeviceChannels { get; }
    public float Volume { get => _ring.Volume; set => _ring.Volume = Math.Clamp(value, 0f, 2f); }
    public int DriftCorrections { get; private set; }
    public int BufferedSamples => _ring.Count;

    public event Action<OutputSink, Exception?>? Stopped;

    public OutputSink(DeviceManager devices, string deviceId, int sourceSampleRate, int bufferMs)
    {
        _device = devices.GetMMDevice(deviceId) ?? throw new InvalidOperationException("Output device not found");
        DeviceId = deviceId;
        Name = _device.FriendlyName;
        var mix = _device.AudioClient.MixFormat;
        DeviceSampleRate = mix.SampleRate;
        DeviceChannels = Math.Max(1, mix.Channels);

        _targetFill = sourceSampleRate * bufferMs * 2 / 1000;
        _highWater = sourceSampleRate * bufferMs * 8 / 1000;
        _ring = new MonoRing(sourceSampleRate, sourceSampleRate * 4);

        ISampleProvider chain = _ring;
        if (DeviceSampleRate != sourceSampleRate)
            chain = new WdlResamplingSampleProvider(chain, DeviceSampleRate);
        chain = DeviceChannels switch
        {
            1 => chain,
            2 => new MonoToStereoSampleProvider(chain),
            _ => new MonoToMultiChannelProvider(chain, DeviceChannels)
        };

        _out = new WasapiOut(_device, AudioClientShareMode.Shared, true, Math.Max(bufferMs * 2, 30));
        _out.PlaybackStopped += OnPlaybackStopped;
        _out.Init(chain.ToWaveProvider());
        // pre-fill so the first WASAPI pulls do not underrun
        _ring.WriteZeros(_targetFill);
        _out.Play();
        Log.Info("Output", $"Sink started: {Name} ({DeviceSampleRate} Hz, {DeviceChannels} ch, src {sourceSampleRate} Hz, buffer {bufferMs} ms)");
    }

    private int _dropPhase;

    public void Write(ReadOnlySpan<float> mono)
    {
        if (_disposed) return;
        int fill = _ring.Count;
        if (fill > _highWater * 3)
        {
            // pathological backlog (device stalled): hard reset to the target fill
            _ring.Discard(fill - _targetFill);
            DriftCorrections++;
            Log.Debug("Output", $"{Name}: backlog reset #{DriftCorrections} ({fill} samples)");
            _ring.Write(mono);
            return;
        }
        if (fill > _targetFill + _targetFill / 2)
        {
            // Render clock slightly slower than capture clock: shed one sample per 96 (about 1 %)
            // until the backlog is back at target. Inaudible for speech, unlike dropping whole chunks.
            Span<float> thinned = stackalloc float[mono.Length];
            int n = 0;
            for (int i = 0; i < mono.Length; i++)
            {
                if (++_dropPhase >= 96) { _dropPhase = 0; continue; }
                thinned[n++] = mono[i];
            }
            DriftCorrections++;
            _ring.Write(thinned.Slice(0, n));
            return;
        }
        _ring.Write(mono);
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (_disposed) return;
        if (e.Exception != null) Log.Warn("Output", $"Sink {Name} stopped: {e.Exception.Message}");
        Stopped?.Invoke(this, e.Exception);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _out.PlaybackStopped -= OnPlaybackStopped; _out.Stop(); } catch { /* ignore */ }
        try { _out.Dispose(); } catch { /* ignore */ }
        try { _device.Dispose(); } catch { /* ignore */ }
    }

    /// <summary>Thread-safe mono float ring buffer exposed as an ISampleProvider (pads with silence on underrun).</summary>
    private sealed class MonoRing : ISampleProvider
    {
        private readonly float[] _buf;
        private int _read, _write, _count;
        private readonly object _lock = new();

        public MonoRing(int sampleRate, int capacity)
        {
            _buf = new float[capacity];
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        }

        public WaveFormat WaveFormat { get; }
        public float Volume { get; set; } = 1f;
        public int Count { get { lock (_lock) return _count; } }
        public int Underruns { get; private set; }

        public void Write(ReadOnlySpan<float> data)
        {
            lock (_lock)
            {
                foreach (var v in data)
                {
                    if (_count == _buf.Length) { _read = (_read + 1) % _buf.Length; _count--; }
                    _buf[_write] = v;
                    _write = (_write + 1) % _buf.Length;
                    _count++;
                }
            }
        }

        public void WriteZeros(int n)
        {
            Span<float> z = stackalloc float[Math.Min(n, 1024)];
            while (n > 0) { int c = Math.Min(n, z.Length); Write(z.Slice(0, c)); n -= c; }
        }

        public void Discard(int n)
        {
            lock (_lock)
            {
                n = Math.Min(n, _count);
                _read = (_read + n) % _buf.Length;
                _count -= n;
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            lock (_lock)
            {
                int n = Math.Min(count, _count);
                float vol = Volume;
                for (int i = 0; i < n; i++)
                {
                    buffer[offset + i] = _buf[_read] * vol;
                    _read = (_read + 1) % _buf.Length;
                }
                _count -= n;
                if (n < count)
                {
                    Array.Clear(buffer, offset + n, count - n);
                    if (n == 0) Underruns++;
                }
                return count;
            }
        }
    }

    /// <summary>Copies a mono source to N output channels.</summary>
    private sealed class MonoToMultiChannelProvider : ISampleProvider
    {
        private readonly ISampleProvider _src;
        private readonly int _ch;
        private float[] _tmp = Array.Empty<float>();

        public MonoToMultiChannelProvider(ISampleProvider src, int channels)
        {
            _src = src; _ch = channels;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(src.WaveFormat.SampleRate, channels);
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            int frames = count / _ch;
            if (_tmp.Length < frames) _tmp = new float[frames];
            int got = _src.Read(_tmp, 0, frames);
            for (int f = 0; f < got; f++)
                for (int c = 0; c < _ch; c++)
                    buffer[offset + f * _ch + c] = _tmp[f];
            return got * _ch;
        }
    }
}
