using VoiceChanger.Core.Dsp;
using VoiceChanger.Core.Presets;

namespace VoiceChangerPro.AndroidApp.Audio;

/// <summary>
/// Applies a voice to a recorded clip. Uses a fresh VoicePipeline and feeds it in small blocks exactly like
/// the live path, so a clip sounds the same as live monitoring. The pipeline's look-ahead delay is removed
/// so the result lines up with the original, and echo / reverb tails are allowed to ring out.
/// </summary>
public static class OfflineRenderer
{
    public const int BlockSize = 512;

    public static float[] Render(float[] input, VoiceProfile profile, GlobalProcessing global, CancellationToken cancel = default)
    {
        var pipeline = new VoicePipeline(AudioIo.SampleRate);
        pipeline.Apply(profile);
        pipeline.ApplyGlobal(global);
        pipeline.Bypass = false;

        int latency = pipeline.LatencySamples;
        bool hasTail = profile.ReverbMix > 0.001f || profile.EchoMix > 0.001f;
        int tail = hasTail ? (int)(AudioIo.SampleRate * 1.5) : 0;
        int outputLength = input.Length + tail;
        int total = outputLength + latency;

        var output = new float[outputLength];
        var block = new float[BlockSize];
        for (int pos = 0; pos < total; pos += BlockSize)
        {
            cancel.ThrowIfCancellationRequested();
            int n = Math.Min(BlockSize, total - pos);
            for (int i = 0; i < n; i++)
            {
                int src = pos + i;
                block[i] = src < input.Length ? input[src] : 0f;
            }
            pipeline.Process(block.AsSpan(0, n));
            for (int i = 0; i < n; i++)
            {
                int dst = pos + i - latency;
                if (dst >= 0 && dst < outputLength) output[dst] = block[i];
            }
        }
        return output;
    }
}
