using VoiceChangerPro.AndroidApp.Diagnostics;

namespace VoiceChangerPro.AndroidApp.Audio;

/// <summary>
/// Records the raw (unprocessed) microphone signal into memory, up to <see cref="MaxSeconds"/>.
/// Keeping the raw take means any voice can be applied afterwards, as often as the user likes.
/// </summary>
public sealed class ClipRecorder : IDisposable
{
    public const int MaxSeconds = 60;
    private const int Block = 960; // 20 ms

    private readonly float[] _data = new float[AudioIo.SampleRate * MaxSeconds];
    private volatile int _count;
    private Thread? _thread;
    private AudioIo.MicStream? _mic;
    private volatile bool _stopRequested;
    private float _peakHold;

    /// <summary>Raised on the recording thread when capture fails (message is user-facing).</summary>
    public event Action<string>? Failed;

    public bool IsRecording => _thread != null;

    /// <summary>True once the time limit stopped the recording by itself.</summary>
    public bool ReachedLimit { get; private set; }

    public bool SyntheticInput { get; set; }

    public double Seconds => _count / (double)AudioIo.SampleRate;

    public float TakePeak() { float v = _peakHold; _peakHold = 0; return v; }

    /// <summary>Starts recording. Throws AudioUnavailableException when the microphone cannot be used.</summary>
    public void Start()
    {
        if (IsRecording) return;
        var mic = AudioIo.OpenMic(Block);
        try { mic.Start(); }
        catch { mic.Dispose(); throw; }

        _mic = mic;
        _count = 0;
        ReachedLimit = false;
        _stopRequested = false;
        var thread = new Thread(() => Run(mic)) { IsBackground = true, Name = "ClipRecorder" };
        _thread = thread;
        thread.Start();
        AppLog.Info("Record", $"Recording started (source={mic.Source}, float={mic.IsFloat}, synthetic={SyntheticInput})");
    }

    /// <summary>Stops recording and returns the captured samples (with a short fade-in and fade-out).</summary>
    public float[] Stop()
    {
        var thread = _thread;
        if (thread != null)
        {
            _stopRequested = true;
            if (!thread.Join(600))
            {
                _mic?.Stop();
                thread.Join(1000);
            }
            _thread = null;
            _mic = null;
        }
        int count = _count;
        var clip = new float[count];
        Array.Copy(_data, clip, count);
        ApplyFades(clip);
        AppLog.Info("Record", $"Recording stopped: {count / (double)AudioIo.SampleRate:F2} s");
        return clip;
    }

    private void Run(AudioIo.MicStream mic)
    {
        var buffer = new float[Block];
        var synth = SyntheticInput ? new SyntheticVoice(AudioIo.SampleRate, 140, modulate: true) : null;
        string? failure = null;
        try
        {
            while (!_stopRequested)
            {
                int got = mic.Read(buffer, Block);
                if (_stopRequested) break;
                if (got < 0) { failure = $"The microphone stopped working (error {got})."; break; }
                if (got == 0) continue;
                synth?.Fill(buffer.AsSpan(0, got));

                int room = _data.Length - _count;
                int n = Math.Min(room, got);
                Array.Copy(buffer, 0, _data, _count, n);
                float peak = 0;
                for (int i = 0; i < n; i++) { float a = Math.Abs(buffer[i]); if (a > peak) peak = a; }
                if (peak > _peakHold) _peakHold = peak;
                _count += n;
                if (_count >= _data.Length) { ReachedLimit = true; break; }
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Record", "Recording thread crashed", ex);
            failure = "Recording stopped because of an unexpected error.";
        }
        finally
        {
            mic.Dispose();
        }
        if (failure != null && !_stopRequested) Failed?.Invoke(failure);
    }

    /// <summary>10 ms fades remove the click some microphones make when they open or close.</summary>
    private static void ApplyFades(float[] clip)
    {
        int fade = Math.Min(clip.Length / 2, AudioIo.SampleRate / 100);
        for (int i = 0; i < fade; i++)
        {
            float g = i / (float)fade;
            clip[i] *= g;
            clip[clip.Length - 1 - i] *= g;
        }
    }

    public void Dispose()
    {
        if (IsRecording) Stop();
    }
}
