using System.Collections.Concurrent;
using System.Text;

namespace VoiceChanger.Core.Logging;

public enum LogLevel { Debug, Info, Warn, Error }

/// <summary>
/// Lightweight asynchronous file logger with daily rotation and a bounded in-memory tail
/// (shown in the diagnostics panel). Never throws into callers.
/// </summary>
public static class Log
{
    private static readonly BlockingCollection<string> Queue = new(new ConcurrentQueue<string>());
    private static readonly ConcurrentQueue<string> Tail = new();
    private static Thread? _writer;
    private static string? _dir;
    private static readonly object InitLock = new();

    public static LogLevel MinimumLevel { get; set; } = LogLevel.Debug;
    public static event Action<string>? LineWritten;

    public static string Directory => _dir ?? Path.Combine(AppPaths.LocalData, "logs");

    public static void Initialize(string? directory = null)
    {
        lock (InitLock)
        {
            if (_writer != null) return;
            _dir = directory ?? Path.Combine(AppPaths.LocalData, "logs");
            try { System.IO.Directory.CreateDirectory(_dir); } catch { /* ignore */ }
            _writer = new Thread(WriterLoop) { IsBackground = true, Name = "LogWriter", Priority = ThreadPriority.BelowNormal };
            _writer.Start();
            Info("Log", $"Logging started ({_dir})");
        }
    }

    public static void Debug(string source, string message) => Write(LogLevel.Debug, source, message);
    public static void Info(string source, string message) => Write(LogLevel.Info, source, message);
    public static void Warn(string source, string message) => Write(LogLevel.Warn, source, message);
    public static void Error(string source, string message, Exception? ex = null)
        => Write(LogLevel.Error, source, ex == null ? message : $"{message}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");

    public static IReadOnlyList<string> RecentLines => Tail.ToArray();

    private static void Write(LogLevel level, string source, string message)
    {
        if (level < MinimumLevel) return;
        string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level,-5}] [{source}] {message}";
        Tail.Enqueue(line);
        while (Tail.Count > 500 && Tail.TryDequeue(out _)) { }
        try { LineWritten?.Invoke(line); } catch { /* ignore */ }
        if (_writer == null) return;
        try { Queue.Add(line); } catch { /* ignore */ }
    }

    private static void WriterLoop()
    {
        var sb = new StringBuilder();
        while (true)
        {
            try
            {
                string first = Queue.Take();
                sb.Clear();
                sb.AppendLine(first);
                while (Queue.TryTake(out var more, 50)) sb.AppendLine(more);
                string file = Path.Combine(_dir!, $"voicechanger-{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(file, sb.ToString());
                CleanupOld();
            }
            catch
            {
                Thread.Sleep(500);
            }
        }
    }

    private static DateTime _lastCleanup = DateTime.MinValue;
    private static void CleanupOld()
    {
        if ((DateTime.Now - _lastCleanup).TotalHours < 6) return;
        _lastCleanup = DateTime.Now;
        try
        {
            foreach (var f in System.IO.Directory.GetFiles(_dir!, "voicechanger-*.log"))
                if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-14)) File.Delete(f);
        }
        catch { /* ignore */ }
    }

    public static void Flush()
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(800);
        while (Queue.Count > 0 && DateTime.UtcNow < deadline) Thread.Sleep(20);
    }
}

/// <summary>Well-known application folders.</summary>
public static class AppPaths
{
    public const string AppName = "VoiceChangerPro";

    public static string RoamingData => Ensure(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName));
    public static string LocalData => Ensure(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName));
    public static string Presets => Ensure(Path.Combine(RoamingData, "presets"));
    public static string SettingsFile => Path.Combine(RoamingData, "settings.json");

    private static string Ensure(string path)
    {
        try { Directory.CreateDirectory(path); } catch { /* ignore */ }
        return path;
    }
}
