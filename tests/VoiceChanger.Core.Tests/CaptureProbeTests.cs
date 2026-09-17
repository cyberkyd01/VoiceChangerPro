using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using VoiceChanger.Core.Audio.Devices;
using Xunit;
using Xunit.Abstractions;

namespace VoiceChanger.Core.Tests;

/// <summary>Low-level capture diagnostics (VCP_INTEGRATION=1 only).</summary>
public class CaptureProbeTests
{
    private readonly ITestOutputHelper _out;
    public CaptureProbeTests(ITestOutputHelper output) => _out = output;
    private static bool Enabled => Environment.GetEnvironmentVariable("VCP_INTEGRATION") == "1";

    [Fact]
    public void Probe_WasapiFlags_And_WinMm()
    {
        if (!Enabled) return;
        using var dm = new DeviceManager();
        foreach (var mic in dm.GetCaptureDevices().Where(d => !d.IsVirtual))
        {
            using var dev = dm.GetMMDevice(mic.Id)!;
            _out.WriteLine($"=== {mic.Name}");

            // 1) raw IAudioClient / IAudioCaptureClient, polling mode
            try
            {
                var client = dev.AudioClient;
                var fmt = client.MixFormat;
                client.Initialize(AudioClientShareMode.Shared, AudioClientStreamFlags.None, 10_000_000 / 10, 0, fmt, Guid.Empty);
                var cc = client.AudioCaptureClient;
                client.Start();
                int packets = 0, silentPackets = 0, frames = 0; float peak = 0; var flagsSeen = new HashSet<AudioClientBufferFlags>();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < 1500)
                {
                    int next = cc.GetNextPacketSize();
                    if (next == 0) { Thread.Sleep(5); continue; }
                    IntPtr p = cc.GetBuffer(out int nFrames, out AudioClientBufferFlags flags);
                    packets++; frames += nFrames; flagsSeen.Add(flags);
                    if ((flags & AudioClientBufferFlags.Silent) != 0) silentPackets++;
                    else
                    {
                        int n = nFrames * fmt.Channels;
                        var tmp = new float[n];
                        Marshal.Copy(p, tmp, 0, n);
                        foreach (var v in tmp) peak = Math.Max(peak, Math.Abs(v));
                    }
                    cc.ReleaseBuffer(nFrames);
                }
                client.Stop();
                _out.WriteLine($"  WASAPI poll: packets={packets} silentPackets={silentPackets} frames={frames} flags={string.Join('|', flagsSeen)} peak={peak:F5} fmt={fmt}");
            }
            catch (Exception ex) { _out.WriteLine("  WASAPI poll failed: " + ex.Message); }

            // 2) legacy WinMM capture (waveIn) by matching product name
            try
            {
                int devNo = -1;
                for (int i = 0; i < WaveInEvent.DeviceCount; i++)
                {
                    var caps = WaveInEvent.GetCapabilities(i);
                    if (mic.Name.StartsWith(caps.ProductName.Substring(0, Math.Min(caps.ProductName.Length, 20)), StringComparison.OrdinalIgnoreCase)) { devNo = i; break; }
                }
                _out.WriteLine($"  WinMM device index: {devNo} (of {WaveInEvent.DeviceCount})");
                if (devNo >= 0)
                {
                    using var win = new WaveInEvent { DeviceNumber = devNo, WaveFormat = new WaveFormat(44100, 16, 1), BufferMilliseconds = 50 };
                    int peak16 = 0; long bytes = 0;
                    win.DataAvailable += (_, e) =>
                    {
                        bytes += e.BytesRecorded;
                        for (int i = 0; i + 1 < e.BytesRecorded; i += 2) peak16 = Math.Max(peak16, Math.Abs((int)BitConverter.ToInt16(e.Buffer, i)));
                    };
                    win.StartRecording();
                    Thread.Sleep(1500);
                    win.StopRecording();
                    _out.WriteLine($"  WinMM: bytes={bytes} peak16={peak16} ({(peak16 > 0 ? 20 * Math.Log10(peak16 / 32768.0) : double.NegativeInfinity):F1} dBFS)");
                }
            }
            catch (Exception ex) { _out.WriteLine("  WinMM failed: " + ex.Message); }
        }
    }
}
