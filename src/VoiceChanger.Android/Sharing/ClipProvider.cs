using Android.Content;
using Android.Database;
using Android.OS;
using Android.Provider;

namespace VoiceChangerPro.AndroidApp.Sharing;

/// <summary>
/// Minimal read-only ContentProvider that hands exported clips (cache/clips/*.wav) to other apps through
/// content:// URIs with a temporary read grant, which is what ACTION_SEND needs on modern Android.
/// A tiny replacement for androidx FileProvider so the app needs no extra packages.
/// </summary>
[ContentProvider(new[] { Authority }, Name = "com.cyberkyd.voicechangerpro.ClipProvider", Exported = false, GrantUriPermissions = true)]
public sealed class ClipProvider : ContentProvider
{
    public const string Authority = "com.cyberkyd.voicechangerpro.clips";
    public const string MimeType = "audio/x-wav";

    public static string ClipsDirectory(Context context)
    {
        var dir = Path.Combine(context.CacheDir!.AbsolutePath, "clips");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static Android.Net.Uri UriFor(string fileName) =>
        Android.Net.Uri.Parse($"content://{Authority}/{Android.Net.Uri.Encode(fileName)}")!;

    public override bool OnCreate() => true;

    /// <summary>Maps a URI to a file inside the clips folder; rejects anything else (no path tricks).</summary>
    private string? Resolve(Android.Net.Uri uri)
    {
        var name = uri.LastPathSegment;
        if (string.IsNullOrEmpty(name) || name.Contains('/') || name.Contains("..") || Context == null) return null;
        var path = Path.Combine(ClipsDirectory(Context), name);
        return File.Exists(path) ? path : null;
    }

    public override ParcelFileDescriptor? OpenFile(Android.Net.Uri uri, string mode)
    {
        if (mode != "r") throw new Java.Lang.SecurityException("Clips are read-only");
        var path = Resolve(uri) ?? throw new Java.IO.FileNotFoundException(uri.ToString());
        return ParcelFileDescriptor.Open(new Java.IO.File(path), ParcelFileMode.ReadOnly);
    }

    public override ICursor? Query(Android.Net.Uri uri, string[]? projection, string? selection, string[]? selectionArgs, string? sortOrder)
    {
        var path = Resolve(uri);
        if (path == null) return null;
        // Messaging apps ask for the display name and size before copying the file.
        var columns = projection ?? new[] { IOpenableColumns.DisplayName, IOpenableColumns.Size };
        var cols = new List<string>();
        var vals = new List<Java.Lang.Object>();
        foreach (var c in columns)
        {
            if (c == IOpenableColumns.DisplayName) { cols.Add(c); vals.Add(new Java.Lang.String(Path.GetFileName(path))); }
            else if (c == IOpenableColumns.Size) { cols.Add(c); vals.Add(Java.Lang.Long.ValueOf(new FileInfo(path).Length)); }
        }
        var cursor = new MatrixCursor(cols.ToArray(), 1);
        cursor.AddRow(vals.ToArray());
        return cursor;
    }

    public override string? GetType(Android.Net.Uri uri) => MimeType;

    public override Android.Net.Uri? Insert(Android.Net.Uri uri, ContentValues? values) => throw new Java.Lang.UnsupportedOperationException();
    public override int Delete(Android.Net.Uri uri, string? selection, string[]? selectionArgs) => throw new Java.Lang.UnsupportedOperationException();
    public override int Update(Android.Net.Uri uri, ContentValues? values, string? selection, string[]? selectionArgs) => throw new Java.Lang.UnsupportedOperationException();
}
