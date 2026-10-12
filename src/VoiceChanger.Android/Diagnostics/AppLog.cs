using ALog = Android.Util.Log;

namespace VoiceChangerPro.AndroidApp.Diagnostics;

/// <summary>Thin logcat wrapper. Everything is logged under the tag "VoiceChangerPro".</summary>
public static class AppLog
{
    public const string Tag = "VoiceChangerPro";

    /// <summary>Periodic audio statistics. On in Debug builds, or via the "verbose" launch extra.</summary>
    public static bool Verbose { get; set; }
#if DEBUG
        = true;
#endif

    public static void Info(string area, string message) => ALog.Info(Tag, $"[{area}] {message}");
    public static void Warn(string area, string message) => ALog.Warn(Tag, $"[{area}] {message}");

    public static void Error(string area, string message, Exception? ex = null)
        => ALog.Error(Tag, ex == null ? $"[{area}] {message}" : $"[{area}] {message}: {ex}");

    public static void Stats(string area, string message)
    {
        if (Verbose) ALog.Info(Tag, $"[{area}] {message}");
    }
}
