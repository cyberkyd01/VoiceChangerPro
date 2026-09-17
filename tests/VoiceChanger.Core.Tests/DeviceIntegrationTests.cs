using VoiceChanger.Core.Audio.Devices;
using VoiceChanger.Core.Audio.Engine;
using VoiceChanger.Core.Presets;
using VoiceChanger.Core.Settings;
using Xunit;
using Xunit.Abstractions;

namespace VoiceChanger.Core.Tests;

/// <summary>
/// Real-device smoke tests. They only run when the environment variable VCP_INTEGRATION=1 is set,
/// because they open the machine's actual microphone and speakers.
/// </summary>
public class DeviceIntegrationTests
{
    private readonly ITestOutputHelper _out;
    public DeviceIntegrationTests(ITestOutputHelper output) => _out = output;

    private static bool Enabled => Environment.GetEnvironmentVariable("VCP_INTEGRATION") == "1";

    [Fact]
    public void DeviceManager_EnumeratesEndpoints()
    {
        if (!Enabled) return;
        using var dm = new DeviceManager();
        var caps = dm.GetCaptureDevices();
        var rends = dm.GetRenderDevices();
        foreach (var d in caps) _out.WriteLine($"CAP  {d.Name} | {d.AdapterName} | {d.SampleRate} Hz {d.Channels} ch | default={d.IsDefault} virtual={d.IsVirtual}");
        foreach (var d in rends) _out.WriteLine($"REND {d.Name} | {d.AdapterName} | {d.SampleRate} Hz {d.Channels} ch | default={d.IsDefault} virtual={d.IsVirtual}");
        var cables = VirtualCableDetector.Detect(rends, caps);
        foreach (var c in cables) _out.WriteLine($"CABLE {c.DisplayName}");
        Assert.NotEmpty(caps);
        Assert.NotEmpty(rends);
    }

    [Fact]
    public void RawCapture_ReportsPeakLevel()
    {
        if (!Enabled) return;
        using var dm = new DeviceManager();
        foreach (var mic in dm.GetCaptureDevices().Where(d => !d.IsVirtual))
        {
        using var dev = dm.GetMMDevice(mic!.Id)!;
        _out.WriteLine($"[{mic.Name}] mute={dev.AudioEndpointVolume.Mute} volume={dev.AudioEndpointVolume.MasterVolumeLevelScalar:P0}");
        using var cap = new NAudio.CoreAudioApi.WasapiCapture(dev, true, 20);
        var fmt = cap.WaveFormat;
        float rawPeak = 0; long bytes = 0;
        cap.DataAvailable += (_, e) =>
        {
            bytes += e.BytesRecorded;
            if (fmt.BitsPerSample == 32)
            {
                var f = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(e.Buffer.AsSpan(0, e.BytesRecorded));
                foreach (var v in f) rawPeak = Math.Max(rawPeak, Math.Abs(v));
            }
        };
        cap.StartRecording();
        Thread.Sleep(1500);
        cap.StopRecording();
        _out.WriteLine($"format={fmt} bytes={bytes} rawPeak={rawPeak:F5}");
        Assert.True(bytes > 0, "no audio data delivered by WASAPI");
        }
    }

    [Fact]
    public void VoiceEngine_CapturesProcessesAndRendersToDefaultOutput()
    {
        if (!Enabled) return;
        using var dm = new DeviceManager();
        var mics = dm.GetCaptureDevices().Where(d => !d.IsVirtual).ToList();
        var mic = mics.FirstOrDefault(d => d.Name.Contains("Headset", StringComparison.OrdinalIgnoreCase)) ?? mics.FirstOrDefault();
        var spk = dm.GetRenderDevices().FirstOrDefault(d => !d.IsVirtual && d.Name.Contains("Headset", StringComparison.OrdinalIgnoreCase))
                  ?? dm.GetRenderDevices().FirstOrDefault(d => !d.IsVirtual);
        Assert.NotNull(mic);
        Assert.NotNull(spk);
        _out.WriteLine($"mic={mic!.Name} spk={spk!.Name}");

        var cfg = new MicrophoneConfig { DeviceId = mic!.Id, DeviceName = mic.Name, Active = true, MonitorEnabled = true, MonitorVolume = 0.0f };
        var preset = BuiltInPresets.All.First(p => p.Name == "American FM Radio");
        cfg.Profile.CopyFrom(preset.Profile, notify: false);

        using var engine = new VoiceEngine(dm, cfg, 20);
        // route to the speaker as the "virtual" output and also as the monitor (volume 0 so the test is silent)
        bool ok = engine.Start(spk!.Id, spk.Id, monitorEnabled: true);
        _out.WriteLine($"start ok={ok} state={engine.State} err={engine.LastError} rate={engine.SampleRate} ch={engine.InputChannels} latency≈{engine.EstimatedLatencyMs:F0} ms");
        Assert.True(ok, engine.LastError);
        Assert.Equal(EngineState.Running, engine.State);
        Assert.True(engine.VirtualActive);
        Assert.True(engine.MonitorActive);

        for (int i = 0; i < 6; i++)
        {
            Thread.Sleep(500);
            var pp = engine.Pipeline!;
            _out.WriteLine($"t={i * 0.5:F1}s inPeak={pp.InputPeak:F5} inRms={pp.InputRms:F5} outPeak={pp.OutputPeak:F5} gate={pp.GateOpen} gr={pp.CompressorGainReductionDb:F1}");
        }
        // change parameters live while running
        Thread.Sleep(500);
        engine.Profile.PitchSemitones = -6;
        engine.Profile.ReverbMix = 0.3f;
        engine.EffectEnabled = false;
        Thread.Sleep(500);
        engine.EffectEnabled = true;
        engine.RecordClip(1.0);
        Thread.Sleep(1500);
        Assert.True(engine.HasClip, "test clip should have been recorded");
        engine.LoopClip = true;
        Thread.Sleep(1000);

        var pipe = engine.Pipeline!;
        _out.WriteLine($"input peak={pipe.InputPeak:F4} output peak={pipe.OutputPeak:F4} pitch={pipe.DetectedPitchHz:F0} Hz gate={pipe.GateOpen}");
        Assert.Equal(EngineState.Running, engine.State);
        engine.Stop();
        Assert.Equal(EngineState.Stopped, engine.State);
    }
}
