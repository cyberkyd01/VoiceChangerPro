using NAudio.MediaFoundation;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using VoiceChanger.Core.Logging;

namespace VoiceChanger.Core.Audio.Engine;

/// <summary>
/// Streams an audio file (any format Windows Media Foundation can decode: MP3, WAV, M4A/AAC, WMA, FLAC,
/// MP4 audio, AIFF …) as mono float at the engine sample rate and mixes it into the microphone channel
/// AFTER the voice chain, so the voice effects never touch it. Decoding runs on a background thread
/// into a ring buffer; the audio callback only pulls samples. Supports loop, pause/resume, seek and a
/// smoothed volume. Long files are streamed, not loaded into memory.
/// </summary>
public sealed class BackgroundTrack : IDisposable
{
    private readonly string _path;
    private readonly int _sampleRate;
    private readonly float[] _ring;
    private int _ringRead, _ringWrite, _ringCount;
    private readonly object _lock = new();
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _wake = new(true);
    private volatile bool _disposed;
    private volatile bool _playing;
    private volatile bool _loop = true;
    private volatile bool _seekRequested;
    private double _seekSeconds;
    private float _volume = 0.5f, _volumeCurrent = 0.5f;
    private long _samplesOutput;
    private volatile bool _ended;

    public string FilePath => _path;
    public string FileName => Path.GetFileName(_path);
    public double DurationSeconds { get; private set; }
    public string? Error { get; private set; }
    public bool IsReady { get; private set; }
    public bool Playing { get => _playing; set { _playing = value; _ended = false; _wake.Set(); } }
    public bool Loop { get => _loop; set { _loop = value; _ended = false; _wake.Set(); } }
    public float Volume { get => _volume; set => _volume = Math.Clamp(value, 0f, 1.5f); }
    /// <summary>True when a non-looping track has played to its end.</summary>
    public bool Ended => _ended;
    /// <summary>Approximate playback position in seconds.</summary>
    public double PositionSeconds => _samplesOutput / (double)_sampleRate;

    public BackgroundTrack(string path, int sampleRate)
    {
        _path = path;
        _sampleRate = sampleRate;
        _ring = new float[sampleRate * 3]; // 3 s of look-ahead
        _thread = new Thread(DecodeLoop) { IsBackground = true, Name = "BackgroundTrack", Priority = ThreadPriority.AboveNormal };
        _thread.Start();
    }

    /// <summary>Seeks to a position (seconds); takes effect on the decode thread.</summary>
    public void Seek(double seconds)
    {
        _seekSeconds = Math.Max(0, seconds);
        _seekRequested = true;
        _wake.Set();
    }

    /// <summary>Mixes the next block of the track into <paramref name="buf"/> (additive, with volume).</summary>
    public void MixInto(Span<float> buf)
    {
        if (_disposed || !_playing || !IsReady) return;
        lock (_lock)
        {
            int n = Math.Min(buf.Length, _ringCount);
            for (int i = 0; i < n; i++)
            {
                _volumeCurrent += 0.002f * (_volume - _volumeCurrent);
                buf[i] += _ring[_ringRead] * _volumeCurrent;
                _ringRead = (_ringRead + 1) % _ring.Length;
            }
            _ringCount -= n;
            _samplesOutput += n;
        }
        _wake.Set();
    }

    private void DecodeLoop()
    {
        WaveStream? reader = null;
        try
        {
            try { MediaFoundationApi.Startup(); } catch { /* already started or unavailable */ }
            reader = new MediaFoundationReader(_path);
            DurationSeconds = reader.TotalTime.TotalSeconds;
            ISampleProvider sp = reader.ToSampleProvider();
            if (sp.WaveFormat.Channels == 2) sp = new StereoToMonoSampleProvider(sp) { LeftVolume = 0.5f, RightVolume = 0.5f };
            else if (sp.WaveFormat.Channels > 2) sp = new MultiToMono(sp);
            if (sp.WaveFormat.SampleRate != _sampleRate) sp = new WdlResamplingSampleProvider(sp, _sampleRate);
            IsReady = true;
            Log.Info("Background", $"Loaded '{FileName}' ({DurationSeconds:F0} s, {reader.WaveFormat}) → {_sampleRate} Hz mono");

            var chunk = new float[4096];
            while (!_disposed)
            {
                if (_seekRequested)
                {
                    _seekRequested = false;
                    try
                    {
                        reader.CurrentTime = TimeSpan.FromSeconds(Math.Min(_seekSeconds, Math.Max(0, DurationSeconds - 0.05)));
                        lock (_lock) { _ringRead = _ringWrite = 0; _ringCount = 0; _samplesOutput = (long)(_seekSeconds * _sampleRate); }
                        _ended = false;
                    }
                    catch (Exception ex) { Log.Warn("Background", "Seek failed: " + ex.Message); }
                }

                int free;
                lock (_lock) free = _ring.Length - _ringCount;
                if (!_playing || _ended || free < chunk.Length)
                {
                    _wake.Reset();
                    _wake.Wait(200);
                    continue;
                }

                int got = sp.Read(chunk, 0, chunk.Length);
                if (got == 0)
                {
                    if (_loop)
                    {
                        reader.Position = 0;
                        lock (_lock) _samplesOutput = 0;
                        continue;
                    }
                    _ended = true;
                    continue;
                }
                lock (_lock)
                {
                    for (int i = 0; i < got; i++)
                    {
                        _ring[_ringWrite] = chunk[i];
                        _ringWrite = (_ringWrite + 1) % _ring.Length;
                    }
                    _ringCount += got;
                }
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            IsReady = false;
            Log.Error("Background", $"Cannot play '{_path}'", ex);
        }
        finally
        {
            reader?.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _playing = false;
        _wake.Set();
        if (!_thread.Join(1500)) Log.Warn("Background", "Decode thread did not stop in time");
        _wake.Dispose();
    }

    /// <summary>Averages N channels down to mono.</summary>
    private sealed class MultiToMono : ISampleProvider
    {
        private readonly ISampleProvider _src;
        private readonly int _ch;
        private float[] _tmp = Array.Empty<float>();
        public MultiToMono(ISampleProvider src) { _src = src; _ch = src.WaveFormat.Channels; WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(src.WaveFormat.SampleRate, 1); }
        public WaveFormat WaveFormat { get; }
        public int Read(float[] buffer, int offset, int count)
        {
            if (_tmp.Length < count * _ch) _tmp = new float[count * _ch];
            int got = _src.Read(_tmp, 0, count * _ch) / _ch;
            for (int f = 0; f < got; f++)
            {
                float s = 0;
                for (int c = 0; c < _ch; c++) s += _tmp[f * _ch + c];
                buffer[offset + f] = s / _ch;
            }
            return got;
        }
    }
}
