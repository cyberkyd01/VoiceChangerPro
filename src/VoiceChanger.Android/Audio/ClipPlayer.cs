using Android.Media;
using VoiceChangerPro.AndroidApp.Diagnostics;

namespace VoiceChangerPro.AndroidApp.Audio;

/// <summary>Plays a mono float clip on a background thread. One clip at a time.</summary>
public sealed class ClipPlayer : IDisposable
{
    private const int Chunk = 4096;
    private Thread? _thread;
    private AudioTrack? _track;
    private volatile bool _stopRequested;
    private int _totalFrames;

    public bool IsPlaying => _thread != null;

    /// <summary>Playback position from 0 to 1.</summary>
    public double Progress
    {
        get
        {
            var t = _track;
            if (t == null || _totalFrames == 0) return 0;
            try { return Math.Clamp(t.PlaybackHeadPosition / (double)_totalFrames, 0, 1); }
            catch { return 0; }
        }
    }

    public void Play(float[] samples)
    {
        Stop();
        if (samples.Length == 0) return;
        var track = AudioIo.OpenSpeaker(Chunk * 2, lowLatency: false);
        _track = track;
        _totalFrames = samples.Length;
        _stopRequested = false;
        var thread = new Thread(() => Run(track, samples)) { IsBackground = true, Name = "ClipPlayer" };
        _thread = thread;
        thread.Start();
    }

    private void Run(AudioTrack track, float[] samples)
    {
        try
        {
            track.Play();
            int pos = 0;
            while (pos < samples.Length && !_stopRequested)
            {
                int n = Math.Min(Chunk, samples.Length - pos);
                int w = track.Write(samples, pos, n, WriteMode.Blocking);
                if (w < 0) { AppLog.Warn("Player", $"Write failed: {w}"); break; }
                pos += w;
            }
            // Let the tail of the buffer play out before stopping.
            while (!_stopRequested && track.PlaybackHeadPosition < samples.Length && track.PlayState == PlayState.Playing)
                Thread.Sleep(20);
        }
        catch (Exception ex)
        {
            AppLog.Error("Player", "Playback failed", ex);
        }
        finally
        {
            try { track.Pause(); track.Flush(); track.Stop(); } catch { /* ignore */ }
        }
    }

    /// <summary>True when the clip reached its end by itself (the UI then calls Stop to clean up).</summary>
    public bool Finished => _thread != null && !_thread.IsAlive;

    public void Stop()
    {
        var thread = _thread;
        if (thread != null)
        {
            _stopRequested = true;
            try { _track?.Pause(); _track?.Flush(); } catch { /* unblocks a pending write */ }
            thread.Join(1000);
            _thread = null;
        }
        var track = _track;
        _track = null;
        if (track != null)
        {
            try { track.Release(); track.Dispose(); } catch { /* ignore */ }
        }
    }

    public void Dispose() => Stop();
}
