using Android.Content;
using Android.Media;
using VoiceChangerPro.AndroidApp.Diagnostics;

namespace VoiceChangerPro.AndroidApp.Audio;

/// <summary>Thrown when the microphone or speaker cannot be opened. The message is user-facing.</summary>
public sealed class AudioUnavailableException(string message) : Exception(message);

/// <summary>
/// Opens microphone and speaker streams with the settings every part of the app shares:
/// 48 kHz mono, float samples where the device supports them.
/// </summary>
public static class AudioIo
{
    /// <summary>All capture, processing, playback and export runs at this rate.</summary>
    public const int SampleRate = 48000;

    /// <summary>AudioRecord.READ_BLOCKING (the binding exposes the read mode as a plain int).</summary>
    private const int ReadBlocking = 0;

    /// <summary>The output burst size of the device (frames per hardware buffer). 256 if unknown.</summary>
    public static int FramesPerBurst(Context context)
    {
        try
        {
            var am = (AudioManager?)context.GetSystemService(Context.AudioService);
            if (int.TryParse(am?.GetProperty(AudioManager.PropertyOutputFramesPerBuffer), out int frames) && frames is >= 32 and <= 2048)
                return frames;
        }
        catch (Exception ex)
        {
            AppLog.Warn("Audio", $"Could not read the output burst size: {ex.Message}");
        }
        return 256;
    }

    /// <summary>
    /// Live processing block: a whole number of output bursts (so reads and writes stay aligned with the
    /// hardware), at least 192 frames (4 ms) to keep per-call overhead low.
    /// </summary>
    public static int LiveBlockSize(int burst)
    {
        int block = burst;
        while (block < 192) block += burst;
        return block;
    }

    /// <summary>
    /// Opens the microphone. VOICE_RECOGNITION is tried first: Android requires it to be delivered without
    /// automatic gain control or noise suppression, which gives the voice engine the cleanest raw signal.
    /// Falls back to MIC and DEFAULT, and from float to 16-bit samples, for unusual devices.
    /// </summary>
    public static MicStream OpenMic(int blockFrames)
    {
        var sources = new[] { AudioSource.VoiceRecognition, AudioSource.Mic, AudioSource.Default };
        var encodings = new[] { Encoding.PcmFloat, Encoding.Pcm16bit };
        foreach (var source in sources)
        {
            foreach (var encoding in encodings)
            {
                int min = AudioRecord.GetMinBufferSize(SampleRate, ChannelIn.Mono, encoding);
                if (min <= 0) continue;
                int bytesPerFrame = encoding == Encoding.PcmFloat ? 4 : 2;
                // Generous capture buffer: it only adds latency if the audio thread falls behind,
                // and it prevents lost input during a garbage collection or a UI hiccup.
                int size = Math.Max(min * 2, blockFrames * bytesPerFrame * 8);
                AudioRecord? record = null;
                try
                {
                    record = new AudioRecord(source, SampleRate, ChannelIn.Mono, encoding, size);
                    if (record.State == State.Initialized)
                    {
                        AppLog.Info("Audio", $"Microphone opened: source={source}, encoding={encoding}, buffer={size} bytes");
                        return new MicStream(record, encoding == Encoding.PcmFloat, source);
                    }
                }
                catch (Exception ex)
                {
                    AppLog.Warn("Audio", $"Microphone source {source}/{encoding} failed: {ex.Message}");
                }
                try { record?.Release(); record?.Dispose(); } catch { /* ignore */ }
            }
        }
        throw new AudioUnavailableException("The microphone could not be opened. Another app may be using it.");
    }

    /// <summary>Opens a mono float output stream for speech.</summary>
    public static AudioTrack OpenSpeaker(int bufferFrames, bool lowLatency)
    {
        try
        {
            var attributes = new AudioAttributes.Builder()
                .SetUsage(AudioUsageKind.Media)!
                .SetContentType(AudioContentType.Speech)!
                .Build()!;
            var format = new AudioFormat.Builder()
                .SetEncoding(Encoding.PcmFloat)!
                .SetSampleRate(SampleRate)!
                .SetChannelMask(ChannelOut.Mono)!
                .Build()!;
            int minBytes = AudioTrack.GetMinBufferSize(SampleRate, ChannelOut.Mono, Encoding.PcmFloat);
            var builder = new AudioTrack.Builder()
                .SetAudioAttributes(attributes)!
                .SetAudioFormat(format)!
                .SetTransferMode(AudioTrackMode.Stream)!
                .SetBufferSizeInBytes(Math.Max(minBytes, bufferFrames * 4))!;
            if (lowLatency) builder.SetPerformanceMode(AudioTrackPerformanceMode.LowLatency);
            var track = builder.Build()!;
            if (track.State != AudioTrackState.Initialized)
            {
                track.Release();
                throw new AudioUnavailableException("The speaker or headphones could not be opened.");
            }
            return track;
        }
        catch (AudioUnavailableException) { throw; }
        catch (Exception ex)
        {
            AppLog.Error("Audio", "Opening the output failed", ex);
            throw new AudioUnavailableException("The speaker or headphones could not be opened.");
        }
    }

    /// <summary>A started-or-not microphone stream that always delivers float samples.</summary>
    public sealed class MicStream : IDisposable
    {
        private readonly AudioRecord _record;
        private short[]? _shorts;

        internal MicStream(AudioRecord record, bool isFloat, AudioSource source)
        {
            _record = record;
            IsFloat = isFloat;
            Source = source;
        }

        public bool IsFloat { get; }
        public AudioSource Source { get; }

        /// <summary>Starts capture. Throws a user-facing error when the system refuses (mic busy).</summary>
        public void Start()
        {
            _record.StartRecording();
            if (_record.RecordingState != RecordState.Recording)
                throw new AudioUnavailableException("The microphone is busy. Close other apps that record audio (calls, voice assistants) and try again.");
        }

        /// <summary>Blocking read that fills the whole buffer. Returns the frame count, or a negative error code.</summary>
        public int Read(float[] buffer, int frames)
        {
            int done = 0;
            while (done < frames)
            {
                int n;
                if (IsFloat)
                {
                    n = _record.Read(buffer, done, frames - done, ReadBlocking);
                }
                else
                {
                    if (_shorts == null || _shorts.Length < frames) _shorts = new short[frames];
                    n = _record.Read(_shorts, 0, frames - done);
                    for (int i = 0; i < n; i++) buffer[done + i] = _shorts[i] / 32768f;
                }
                if (n < 0) return n;
                if (n == 0) break;
                done += n;
            }
            return done;
        }

        /// <summary>Stops capture; safe to call from another thread to unblock a pending read.</summary>
        public void Stop()
        {
            try { if (_record.RecordingState == RecordState.Recording) _record.Stop(); } catch { /* ignore */ }
        }

        public void Dispose()
        {
            Stop();
            try { _record.Release(); _record.Dispose(); } catch { /* ignore */ }
        }
    }
}
