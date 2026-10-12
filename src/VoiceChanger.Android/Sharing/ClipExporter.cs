using System.Text;
using Android.App;
using Android.Content;
using Android.Media;
using Android.OS;
using Android.Provider;
using VoiceChangerPro.AndroidApp.Audio;
using VoiceChangerPro.AndroidApp.Diagnostics;

namespace VoiceChangerPro.AndroidApp.Sharing;

/// <summary>Turns rendered clips into WAV files, shares them with other apps and saves them to Music.</summary>
public static class ClipExporter
{
    public const string MusicFolder = "Voice Changer Pro";

    public static string FileNameFor(string voiceName)
    {
        var slug = new StringBuilder();
        foreach (char c in voiceName)
        {
            if (char.IsAsciiLetterOrDigit(c)) slug.Append(c);
            else if (slug.Length > 0 && slug[^1] != '-') slug.Append('-');
        }
        var s = slug.ToString().Trim('-');
        if (s.Length > 32) s = s[..32].Trim('-');
        return $"VoiceChangerPro-{(s.Length > 0 ? s : "clip")}-{DateTime.Now:yyyyMMdd-HHmmss}.wav";
    }

    /// <summary>Writes a 16-bit 48 kHz mono WAV into the private share folder. Keeps only the newest few files.</summary>
    public static string WriteShareCopy(Context context, float[] samples, string fileName)
    {
        var dir = ClipProvider.ClipsDirectory(context);
        try
        {
            foreach (var old in new DirectoryInfo(dir).GetFiles("*.wav").OrderByDescending(f => f.LastWriteTimeUtc).Skip(4))
                old.Delete();
        }
        catch (Exception ex) { AppLog.Warn("Export", $"Could not clean old clips: {ex.Message}"); }

        var path = Path.Combine(dir, fileName);
        WavFile.Write(path, samples, AudioIo.SampleRate);
        AppLog.Info("Export", $"Wrote {path} ({new FileInfo(path).Length} bytes)");
        return path;
    }

    /// <summary>Opens the Android share sheet for a file written by <see cref="WriteShareCopy"/>.</summary>
    public static void Share(Activity activity, string fileName)
    {
        var uri = ClipProvider.UriFor(fileName);
        var send = new Intent(Intent.ActionSend);
        send.SetType(ClipProvider.MimeType);
        send.PutExtra(Intent.ExtraStream, uri);
        // ClipData carries the read grant through the chooser to whichever app the user picks.
        send.ClipData = ClipData.NewRawUri(fileName, uri);
        send.AddFlags(ActivityFlags.GrantReadUriPermission);
        var chooser = Intent.CreateChooser(send, "Share your voice clip")!;
        chooser.AddFlags(ActivityFlags.GrantReadUriPermission);
        activity.StartActivity(chooser);
    }

    /// <summary>True when saving needs the legacy storage permission (Android 8 and 9).</summary>
    public static bool NeedsStoragePermission => Build.VERSION.SdkInt < BuildVersionCodes.Q;

    /// <summary>Saves the clip to Music/Voice Changer Pro and returns a user-facing location.</summary>
    public static string SaveToMusic(Context context, float[] samples, string fileName)
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
        {
            var resolver = context.ContentResolver!;
            var values = new ContentValues();
            // Column names of MediaStore.MediaColumns.
            values.Put("_display_name", fileName);
            values.Put("mime_type", ClipProvider.MimeType);
            values.Put("relative_path", $"{Android.OS.Environment.DirectoryMusic}/{MusicFolder}");
            values.Put("is_pending", 1);
            var collection = MediaStore.Audio.Media.GetContentUri(MediaStore.VolumeExternalPrimary)!;
            var item = resolver.Insert(collection, values) ?? throw new IOException("The Music folder is not available.");
            try
            {
                using (var stream = resolver.OpenOutputStream(item) ?? throw new IOException("Could not open the new file."))
                    WavFile.Write(stream, samples, AudioIo.SampleRate);
                values.Clear();
                values.Put("is_pending", 0);
                resolver.Update(item, values, null, null);
            }
            catch
            {
                try { resolver.Delete(item, null, null); } catch { /* ignore */ }
                throw;
            }
        }
        else
        {
#pragma warning disable CA1422 // legacy path for Android 8 and 9 only
            var music = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryMusic)!.AbsolutePath;
#pragma warning restore CA1422
            var dir = Path.Combine(music, MusicFolder);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, fileName);
            WavFile.Write(path, samples, AudioIo.SampleRate);
            MediaScannerConnection.ScanFile(context, new[] { path }, new[] { ClipProvider.MimeType }, null);
        }
        AppLog.Info("Export", $"Saved {fileName} to Music/{MusicFolder}");
        return $"Music/{MusicFolder}/{fileName}";
    }
}
