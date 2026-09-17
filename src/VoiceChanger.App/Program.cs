using System.Threading;
using VoiceChanger.Core.Logging;

namespace VoiceChanger.App;

/// <summary>Process entry point: single-instance guard, then the WPF application.</summary>
public static class Program
{
    public const string MutexName = "VoiceChangerPro.SingleInstance.v1";
    public const string ShowEventName = "VoiceChangerPro.ShowWindow.v1";

    [STAThread]
    public static int Main(string[] args)
    {
        using var mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            // Another instance is running: ask it to show its window and exit.
            try
            {
                using var evt = EventWaitHandle.OpenExisting(ShowEventName);
                evt.Set();
            }
            catch { /* the other instance may still be starting */ }
            return 0;
        }

        Log.Initialize();
        Log.Info("App", $"Voice Changer Pro starting (args: {string.Join(' ', args)})");

        var app = new App { StartMinimized = args.Contains("--minimized", StringComparer.OrdinalIgnoreCase) };
        app.InitializeComponent();
        int code = app.Run();
        Log.Info("App", $"Exited with code {code}");
        Log.Flush();
        return code;
    }
}
