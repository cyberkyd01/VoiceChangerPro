using System.Diagnostics;
using Android.Media;
using VoiceChanger.Core.Dsp;
using VoiceChanger.Core.Presets;
using VoiceChangerPro.AndroidApp.Diagnostics;

namespace VoiceChangerPro.AndroidApp.Audio;

/// <summary>
/// Live voice changing: microphone → VoicePipeline → headphones.
///
/// One dedicated thread at URGENT_AUDIO priority does all the work in fixed blocks: a blocking read from
/// AudioRecord paces the loop, the block is processed in place, and a blocking write to a low-latency
/// AudioTrack hands it to the output. The output buffer starts small (two blocks) and grows by one burst
/// whenever the system reports an underrun, so each device settles on the smallest delay it can sustain.
///
/// All public members are called from the UI thread. Parameter changes go through VoicePipeline.Apply,
/// which hands a snapshot to the audio thread without locks.
/// </summary>
public sealed class LiveVoiceEngine : IDisposable
{
    private const string Area = "Live";
    private readonly int _burst;
    private readonly int _block;

    private Thread? _thread;
    private AudioIo.MicStream? _mic;
    private VoicePipeline? _pipeline;
    private volatile bool _stopRequested;

    // Values the UI polls. Written by the audio thread; torn reads of a float are harmless here.
    private float _peakInHold, _peakOutHold;
    private volatile int _bufferFrames;
    private volatile int _underrunAdjustments;

    public LiveVoiceEngine(int framesPerBurst)
    {
        _burst = framesPerBurst;
        _block = AudioIo.LiveBlockSize(framesPerBurst);
    }

    /// <summary>Raised on the audio thread when the stream dies unexpectedly (message is user-facing).</summary>
    public event Action<string>? Failed;

    public bool IsRunning => _thread != null;
    public int BlockSize => _block;

    /// <summary>Test mode: replace the microphone signal with a synthetic voice (the mic is still read for timing).</summary>
    public bool SyntheticInput { get; set; }

    /// <summary>Lower playback level used when the user chose to run without headphones.</summary>
    public bool SpeakerSafetyVolume { get; set; }

    public float DetectedPitchHz => _pipeline?.DetectedPitchHz ?? 0;

    /// <summary>Estimated mouth-to-ear delay: DSP look-ahead plus capture and output buffering.</summary>
    public double EstimatedLatencyMs
    {
        get
        {
            var p = _pipeline;
            if (p == null) return 0;
            double bufferMs = (_bufferFrames + 2 * _block) * 1000.0 / AudioIo.SampleRate;
            return p.LatencyMs + bufferMs;
        }
    }

    public int UnderrunAdjustments => _underrunAdjustments;

    /// <summary>Returns the highest input peak since the last call and resets it.</summary>
    public float TakeInputPeak() { float v = _peakInHold; _peakInHold = 0; return v; }

    /// <summary>Returns the highest output peak since the last call and resets it.</summary>
    public float TakeOutputPeak() { float v = _peakOutHold; _peakOutHold = 0; return v; }

    public void Apply(VoiceProfile profile) => _pipeline?.Apply(profile);
    public void ApplyGlobal(GlobalProcessing global) => _pipeline?.ApplyGlobal(global);

    public void SetBypass(bool bypass)
    {
        var p = _pipeline;
        if (p != null) p.Bypass = bypass;
    }

    /// <summary>Opens the microphone and output and starts processing. Throws AudioUnavailableException.</summary>
    public void Start(VoiceProfile profile, GlobalProcessing global, bool bypass)
    {
        if (IsRunning) return;

        var pipeline = new VoicePipeline(AudioIo.SampleRate);
        pipeline.Apply(profile);
        pipeline.ApplyGlobal(global);
        pipeline.Bypass = bypass;

        var mic = AudioIo.OpenMic(_block);
        AudioTrack track;
        try
        {
            track = AudioIo.OpenSpeaker(_block * 8, lowLatency: true);
        }
        catch
        {
            mic.Dispose();
            throw;
        }

        try
        {
            // Start small; the audio thread grows the buffer if this device needs more.
            int initial = Math.Min(2 * _block, track.BufferCapacityInFrames);
            int actual = track.SetBufferSizeInFrames(initial);
            _bufferFrames = actual > 0 ? actual : track.BufferSizeInFrames;
            if (SpeakerSafetyVolume) track.SetVolume(0.5f);
            mic.Start();
        }
        catch
        {
            mic.Dispose();
            try { track.Release(); track.Dispose(); } catch { /* ignore */ }
            throw;
        }

        _pipeline = pipeline;
        _mic = mic;
        _stopRequested = false;
        _underrunAdjustments = 0;
        _peakInHold = _peakOutHold = 0;

        AppLog.Info(Area, $"Starting: block={_block} burst={_burst} trackBuffer={_bufferFrames}/{track.BufferCapacityInFrames} " +
                          $"perf={track.PerformanceMode} dspLatency={pipeline.LatencyMs:F1}ms synthetic={SyntheticInput}");

        var thread = new Thread(() => Run(mic, track, pipeline))
        {
            IsBackground = true,
            Name = "VoiceAudio",
            Priority = ThreadPriority.Highest
        };
        _thread = thread;
        thread.Start();
    }

    /// <summary>Stops processing and releases the microphone and output. Safe to call repeatedly.</summary>
    public void Stop()
    {
        var thread = _thread;
        if (thread == null) return;
        _stopRequested = true;
        if (!thread.Join(600))
        {
            // A read that never returns (driver problem): stopping the recorder unblocks it.
            AppLog.Warn(Area, "Audio thread did not stop in time; forcing the microphone to stop");
            _mic?.Stop();
            thread.Join(1000);
        }
        _thread = null;
        _mic = null;
        AppLog.Info(Area, "Stopped");
    }

    private void Run(AudioIo.MicStream mic, AudioTrack track, VoicePipeline pipeline)
    {
        try { Android.OS.Process.SetThreadPriority(Android.OS.ThreadPriority.UrgentAudio); }
        catch (Exception ex) { AppLog.Warn(Area, $"Could not raise audio thread priority: {ex.Message}"); }

        int block = _block;
        var buffer = new float[block];
        var silence = new float[Math.Max(block, _burst)];
        var synth = SyntheticInput ? new SyntheticVoice(AudioIo.SampleRate, 140, modulate: true) : null;
        string? failure = null;

        long processTicks = 0, blocks = 0, maxTicks = 0, statFrames = 0;
        double inEnergy = 0, outEnergy = 0;
        long statsAt = Stopwatch.GetTimestamp();
        try
        {
            // Prime the whole output buffer with silence: the microphone needs a moment to deliver its
            // first block, and an empty output would underrun (click) right at the start.
            for (int primed = 0; primed < _bufferFrames; primed += block)
                track.Write(silence, 0, Math.Min(block, _bufferFrames - primed), WriteMode.NonBlocking);
            track.Play();
            int lastUnderruns = track.UnderrunCount;
            int capacity = track.BufferCapacityInFrames;
            int maxBuffer = Math.Min(capacity, AudioIo.SampleRate / 5); // never more than 200 ms of output buffer

            while (!_stopRequested)
            {
                int got = mic.Read(buffer, block);
                if (_stopRequested) break;
                if (got < 0) { failure = $"The microphone stopped working (error {got})."; break; }
                if (got == 0) continue;

                var span = buffer.AsSpan(0, got);
                synth?.Fill(span);

                long t0 = Stopwatch.GetTimestamp();
                pipeline.Process(span);
                long dt = Stopwatch.GetTimestamp() - t0;
                processTicks += dt; blocks++;
                if (dt > maxTicks) maxTicks = dt;

                if (pipeline.InputPeak > _peakInHold) _peakInHold = pipeline.InputPeak;
                if (pipeline.OutputPeak > _peakOutHold) _peakOutHold = pipeline.OutputPeak;
                inEnergy += (double)pipeline.InputRms * pipeline.InputRms * got;
                for (int i = 0; i < got; i++) outEnergy += buffer[i] * buffer[i];
                statFrames += got;

                int written = track.Write(buffer, 0, got, WriteMode.Blocking);
                if (written < 0) { failure = $"The headphones or speaker stopped working (error {written})."; break; }

                // Underrun = the output ran dry. Give it one more burst of cushion (up to 200 ms).
                int underruns = track.UnderrunCount;
                if (underruns > lastUnderruns)
                {
                    lastUnderruns = underruns;
                    int current = _bufferFrames;
                    if (current + _burst <= maxBuffer)
                    {
                        int size = track.SetBufferSizeInFrames(current + _burst);
                        if (size > 0) _bufferFrames = size;
                        _underrunAdjustments++;
                        AppLog.Warn(Area, $"Output underrun #{underruns}: buffer raised to {_bufferFrames} frames ({_bufferFrames * 1000.0 / AudioIo.SampleRate:F1} ms)");
                    }
                }

                long now = Stopwatch.GetTimestamp();
                if (now - statsAt > Stopwatch.Frequency * 5)
                {
                    double avgMs = processTicks * 1000.0 / Stopwatch.Frequency / Math.Max(1, blocks);
                    double maxMs = maxTicks * 1000.0 / Stopwatch.Frequency;
                    double blockMs = block * 1000.0 / AudioIo.SampleRate;
                    double inDb = 10 * Math.Log10(Math.Max(1e-12, inEnergy / Math.Max(1, statFrames)));
                    double outDb = 10 * Math.Log10(Math.Max(1e-12, outEnergy / Math.Max(1, statFrames)));
                    AppLog.Stats(Area, $"dsp avg={avgMs:F2}ms max={maxMs:F2}ms per {blockMs:F1}ms block (load {avgMs / blockMs * 100:F0}%), " +
                                       $"underruns={underruns}, buffer={_bufferFrames}, rms in={inDb:F1}dBFS out={outDb:F1}dBFS, pitch={pipeline.DetectedPitchHz:F0}Hz");
                    processTicks = 0; blocks = 0; maxTicks = 0; statsAt = now;
                    statFrames = 0; inEnergy = 0; outEnergy = 0;
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Error(Area, "Audio thread crashed", ex);
            failure = "Live audio stopped because of an unexpected error.";
        }
        finally
        {
            try { track.Pause(); track.Flush(); track.Stop(); } catch { /* ignore */ }
            try { track.Release(); track.Dispose(); } catch { /* ignore */ }
            mic.Dispose();
        }

        if (failure != null && !_stopRequested)
        {
            AppLog.Warn(Area, failure);
            Failed?.Invoke(failure);
        }
    }

    public void Dispose() => Stop();
}
