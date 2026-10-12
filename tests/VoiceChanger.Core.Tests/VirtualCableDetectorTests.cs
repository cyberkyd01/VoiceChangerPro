using VoiceChanger.Core.Audio.Devices;
using Xunit;

namespace VoiceChanger.Core.Tests;

/// <summary>
/// Name-based device classification. A wrong guess here sends the processed voice into the user's
/// headphones instead of other apps, or processes a device that is not a microphone.
/// </summary>
public class VirtualCableDetectorTests
{
    private static int _id;

    private static AudioDeviceInfo Render(string name, string adapter = "", int channels = 2)
        => new($"r{++_id}", name, adapter, AudioDeviceKind.Render, false, false, 48000, channels);

    private static AudioDeviceInfo Capture(string name, string adapter = "")
        => new($"c{++_id}", name, adapter, AudioDeviceKind.Capture, false, false, 48000, 2);

    [Fact]
    public void VbCable_IsDetectedAndPaired()
    {
        var input = Render("CABLE Input", "VB-Audio Virtual Cable");
        var output = Capture("CABLE Output", "VB-Audio Virtual Cable");
        var cables = VirtualCableDetector.Detect(new[] { Render("Headphones", "Realtek(R) Audio"), input }, new[] { output });

        var cable = Assert.Single(cables);
        Assert.Equal(input.Id, cable.RenderInput.Id);
        Assert.Equal(output.Id, cable.CaptureOutput?.Id);
        Assert.Equal("VB-CABLE", cable.Vendor);
    }

    [Theory]
    [InlineData("Speakers", "NVIDIA Broadcast", "Microphone", "NVIDIA Broadcast")]
    [InlineData("SteelSeries Sonar - Gaming", "SteelSeries Sonar Virtual Audio Device", "SteelSeries Sonar - Microphone", "SteelSeries Sonar Virtual Audio Device")]
    [InlineData("Speakers", "HyperX Virtual Surround Sound", "Microphone", "HyperX Virtual Surround Sound")]
    [InlineData("Virtual Desktop Audio", "Virtual Desktop Audio", "Virtual Desktop Mic", "Virtual Desktop Audio")]
    public void PlaybackDevicesThatReachTheHeadphones_AreNeverChosenAsCable(string render, string renderAdapter, string capture, string captureAdapter)
    {
        var cables = VirtualCableDetector.Detect(new[] { Render(render, renderAdapter) }, new[] { Capture(capture, captureAdapter) });
        Assert.Empty(cables);
    }

    [Fact]
    public void VbCable_WinsOverEffectProcessors()
    {
        var input = Render("CABLE Input", "VB-Audio Virtual Cable");
        var cables = VirtualCableDetector.Detect(
            new[] { Render("Speakers", "NVIDIA Broadcast"), Render("SteelSeries Sonar - Chat", "SteelSeries Sonar Virtual Audio Device"), input },
            new[] { Capture("Microphone", "NVIDIA Broadcast"), Capture("CABLE Output", "VB-Audio Virtual Cable") });

        Assert.Equal(input.Id, cables[0].RenderInput.Id);
        Assert.All(cables, c => Assert.True(VirtualCableDetector.IsCable(c.RenderInput)));
    }

    [Fact]
    public void EffectProcessors_AreRecognised()
    {
        Assert.True(VirtualCableDetector.IsEffectProcessor(Render("Speakers", "NVIDIA Broadcast")));
        Assert.True(VirtualCableDetector.IsEffectProcessor(Render("SteelSeries Sonar - Gaming", "SteelSeries Sonar Virtual Audio Device")));
        Assert.False(VirtualCableDetector.IsEffectProcessor(Render("CABLE Input", "VB-Audio Virtual Cable")));
        Assert.False(VirtualCableDetector.IsEffectProcessor(Render("Headphones", "Realtek(R) Audio")));
    }

    [Theory]
    [InlineData("Stereo Mix", "Realtek(R) Audio")]
    [InlineData("What U Hear", "Sound Blaster")]
    [InlineData("CABLE Output", "VB-Audio Virtual Cable")]
    [InlineData("Microphone", "NVIDIA Broadcast")]
    public void NonVoiceCaptureEndpoints_AreNotProcessed(string name, string adapter)
        => Assert.True(VirtualCableDetector.LooksVirtual(Capture(name, adapter)));

    [Theory]
    [InlineData("Microphone Array", "Realtek(R) Audio")]
    [InlineData("Headset Microphone", "Jabra Evolve2 30")]
    [InlineData("Microphone", "HyperX Virtual Surround Sound")]
    public void RealMicrophones_AreProcessed(string name, string adapter)
        => Assert.False(VirtualCableDetector.LooksVirtual(Capture(name, adapter)));

    [Fact]
    public void GamingHeadsetSpeakers_AreOfferedForMonitoring()
        => Assert.False(Render("Speakers", "HyperX Virtual Surround Sound").IsVirtual);
}
