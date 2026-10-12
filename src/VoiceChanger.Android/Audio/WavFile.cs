using System.Text;

namespace VoiceChangerPro.AndroidApp.Audio;

/// <summary>Writes mono float samples as a standard 16-bit PCM WAV file.</summary>
public static class WavFile
{
    public static void Write(Stream stream, float[] samples, int sampleRate)
    {
        const short channels = 1, bits = 16;
        int dataBytes = samples.Length * 2;
        using var w = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        w.Write(Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + dataBytes);
        w.Write(Encoding.ASCII.GetBytes("WAVE"));
        w.Write(Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);                                  // fmt chunk size
        w.Write((short)1);                            // PCM
        w.Write(channels);
        w.Write(sampleRate);
        w.Write(sampleRate * channels * bits / 8);    // byte rate
        w.Write((short)(channels * bits / 8));        // block align
        w.Write(bits);
        w.Write(Encoding.ASCII.GetBytes("data"));
        w.Write(dataBytes);

        var bytes = new byte[Math.Min(dataBytes, 64 * 1024)];
        int pos = 0;
        while (pos < samples.Length)
        {
            int n = Math.Min(bytes.Length / 2, samples.Length - pos);
            for (int i = 0; i < n; i++)
            {
                float s = samples[pos + i];
                if (float.IsNaN(s)) s = 0;
                int v = (int)MathF.Round(Math.Clamp(s, -1f, 1f) * 32767f);
                bytes[2 * i] = (byte)(v & 0xFF);
                bytes[2 * i + 1] = (byte)((v >> 8) & 0xFF);
            }
            w.Write(bytes, 0, n * 2);
            pos += n;
        }
        w.Flush();
    }

    public static void Write(string path, float[] samples, int sampleRate)
    {
        using var fs = File.Create(path);
        Write(fs, samples, sampleRate);
    }
}
