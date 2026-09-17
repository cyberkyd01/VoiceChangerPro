using NAudio.Wave;
using VoiceChanger.Core.Audio.Engine;
using Xunit;

namespace VoiceChanger.Core.Tests;

public class BackgroundTrackTests
{
    private static string WriteTestWav(float seconds, int rate, int channels)
    {
        var path = Path.Combine(Path.GetTempPath(), "vcp-bg-" + Guid.NewGuid().ToString("N") + ".wav");
        using var w = new WaveFileWriter(path, WaveFormat.CreateIeeeFloatWaveFormat(rate, channels));
        int n = (int)(seconds * rate);
        var frame = new float[channels];
        for (int i = 0; i < n; i++)
        {
            float v = 0.5f * MathF.Sin(2 * MathF.PI * 440 * i / rate);
            for (int c = 0; c < channels; c++) frame[c] = v;
            w.WriteSamples(frame, 0, channels);
        }
        return path;
    }

    [Fact]
    public void Track_DecodesResamplesMixesAndLoops()
    {
        var path = WriteTestWav(0.5f, 44100, 2);
        try
        {
            using var track = new BackgroundTrack(path, 48000) { Volume = 1f, Loop = true, Playing = true };
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!track.IsReady && track.Error == null && DateTime.UtcNow < deadline) Thread.Sleep(20);
            Assert.Null(track.Error);
            Assert.True(track.IsReady, "track should load");
            Assert.InRange(track.DurationSeconds, 0.45, 0.55);
            Thread.Sleep(300); // let the decoder fill the ring

            // pull 1.5 s of audio: three times the file length → must have looped and stay non-silent
            var block = new float[480];
            double energy = 0; int blocks = 0;
            for (int i = 0; i < 150; i++)
            {
                Array.Clear(block);
                track.MixInto(block);
                foreach (var v in block) energy += v * v;
                blocks++;
                if (i % 10 == 0) Thread.Sleep(15);
            }
            double rms = Math.Sqrt(energy / (blocks * block.Length));
            Assert.InRange(rms, 0.2, 0.45); // 0.5 amplitude sine ≈ 0.354 RMS (volume ramps up over the first blocks)

            // additive mixing: existing content is preserved
            var mix = new float[480];
            for (int i = 0; i < mix.Length; i++) mix[i] = 0.1f;
            track.MixInto(mix);
            Assert.Contains(mix, v => Math.Abs(v - 0.1f) > 0.05f);

            // pause stops mixing
            track.Playing = false;
            Array.Clear(block);
            track.MixInto(block);
            Assert.All(block, v => Assert.Equal(0f, v));
        }
        finally { try { File.Delete(path); } catch { } }
    }

    [Fact]
    public void Track_NonLoop_EndsAndReportsIt()
    {
        var path = WriteTestWav(0.2f, 48000, 1);
        try
        {
            using var track = new BackgroundTrack(path, 48000) { Volume = 1f, Loop = false, Playing = true };
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!track.IsReady && track.Error == null && DateTime.UtcNow < deadline) Thread.Sleep(20);
            Assert.True(track.IsReady);
            var block = new float[480];
            for (int i = 0; i < 60; i++) { track.MixInto(block); if (i % 10 == 0) Thread.Sleep(20); }
            Thread.Sleep(300);
            Assert.True(track.Ended, "non-looping track should report the end");
        }
        finally { try { File.Delete(path); } catch { } }
    }

    [Fact]
    public void Track_MissingFile_ReportsError()
    {
        using var track = new BackgroundTrack(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid() + ".mp3"), 48000);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (track.Error == null && !track.IsReady && DateTime.UtcNow < deadline) Thread.Sleep(20);
        Assert.NotNull(track.Error);
        Assert.False(track.IsReady);
    }
}
