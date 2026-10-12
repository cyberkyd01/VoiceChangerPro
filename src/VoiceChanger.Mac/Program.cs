using Avalonia;
using VoiceChanger.Core.Dsp;
using VoiceChanger.Core.Logging;
using VoiceChanger.Core.Presets;
using VoiceChanger.Mac.Audio;

namespace VoiceChanger.Mac;

internal static class Program
{
    public static string SupportDirectory { get; } = Ensure(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "VoiceChangerPro"));

    public static string LogDirectory { get; } = Ensure(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Logs", "VoiceChangerPro"));

    [STAThread]
    public static int Main(string[] args)
    {
        Log.Initialize(LogDirectory);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error("App", "Unhandled exception", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => { Log.Error("App", "Unobserved task exception", e.Exception); e.SetObserved(); };

        try
        {
            if (args.Contains("--selftest")) return SelfTest.Run(args);
            if (args.Length >= 2 && args[0] == "--screenshot") return IconRenderer.Screenshot(args[1]);
            if (args.Length >= 2 && args[0] == "--render-icon") return IconRenderer.Render(args[1], args.Length > 2 ? int.Parse(args[2]) : 1024);
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            MacAudio.Shutdown();
            Log.Flush();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .With(new MacOSPlatformOptions { ShowInDock = true })
            .LogToTrace();

    private static string Ensure(string path)
    {
        try { Directory.CreateDirectory(path); } catch { /* ignore */ }
        return path;
    }
}

/// <summary>
/// Headless checks used during development and in CI: lists devices, verifies the voice pipeline
/// shifts pitch, and (with --live) runs the real microphone → pipeline → output path for a few seconds
/// at zero output volume.
/// </summary>
internal static class SelfTest
{
    public static int Run(string[] args)
    {
        int failures = 0;

        // 1. DSP: a 200 Hz tone through a +5 semitone voice should come out near 267 Hz.
        const int sr = 48000;
        var pipeline = new VoicePipeline(sr);
        var profile = new VoiceProfile { PitchSemitones = 5 };
        pipeline.Apply(profile);
        var block = new float[256];
        int total = sr * 2;
        var output = new float[total];
        for (int pos = 0; pos < total; pos += block.Length)
        {
            for (int i = 0; i < block.Length; i++)
            {
                double t = (pos + i) / (double)sr;
                block[i] = (float)(0.3 * Math.Sin(2 * Math.PI * 200 * t) + 0.15 * Math.Sin(2 * Math.PI * 400 * t));
            }
            pipeline.Process(block);
            block.CopyTo(output, pos);
        }
        double f = EstimatePitch(output.AsSpan(sr, sr / 2), sr);
        double expected = 200 * Math.Pow(2, 5 / 12.0);
        bool pitchOk = Math.Abs(f - expected) / expected < 0.06;
        Console.WriteLine($"DSP pitch shift: in 200.0 Hz, out {f:F1} Hz (expected {expected:F1}) {(pitchOk ? "OK" : "FAIL")}");
        if (!pitchOk) failures++;
        Console.WriteLine($"Presets: {BuiltInPresets.All.Count} built-in");

        // 2. Devices
        var devices = MacAudio.Devices();
        Console.WriteLine($"Devices ({devices.Count}), default in #{MacAudio.DefaultInputIndex}, default out #{MacAudio.DefaultOutputIndex}:");
        foreach (var d in devices)
            Console.WriteLine($"  #{d.Index} {d.Name} | in {d.MaxInputChannels} out {d.MaxOutputChannels} | {d.DefaultSampleRate} Hz | cable={d.IsCable} virtual={d.IsVirtual}");
        Console.WriteLine($"Core Audio default input: {CoreAudio.DefaultInputName}, default output: {CoreAudio.DefaultOutputName}");

        // 3. Live path (optional): real microphone through the pipeline into the default output at volume 0.
        if (args.Contains("--live"))
        {
            var mic = devices.FirstOrDefault(d => d.Index == MacAudio.DefaultInputIndex && !d.IsVirtual) ?? devices.First(d => d.HasInput && !d.IsVirtual);
            int rateArg = Array.IndexOf(args, "--rate");
            if (rateArg >= 0 && rateArg + 1 < args.Length) mic = mic with { DefaultSampleRate = double.Parse(args[rateArg + 1]) }; // forces sample-rate conversion
            var outDev = devices.FirstOrDefault(d => d.Index == MacAudio.DefaultOutputIndex) ?? devices.First(d => d.HasOutput);
            var cable = devices.Where(d => d.HasOutput && d.IsCable).OrderBy(d => DeviceClassifier.CableRank(d.Name)).FirstOrDefault();
            using var engine = new MacVoiceEngine();
            bool ok = engine.Start(mic, cable, outDev, 0f, profile, new GlobalProcessing(0.5f, 0f), bypass: false);
            Console.WriteLine($"Live start on '{mic.Name}' → cable '{cable?.Name ?? "none"}', monitor '{outDev.Name}' (muted): {(ok ? "OK" : "FAIL " + engine.LastError)}");
            if (!ok) return failures + 1;
            float maxIn = 0;
            for (int i = 0; i < 30; i++)
            {
                Thread.Sleep(100);
                maxIn = Math.Max(maxIn, engine.Pipeline?.InputPeak ?? 0);
            }
            long callbacks = engine.CallbackCount;
            double expectedCallbacks = 3.0 * engine.SampleRate / MacVoiceEngine.FramesPerBuffer;
            bool flowing = callbacks > expectedCallbacks * 0.8;
            Console.WriteLine($"Callbacks in 3 s: {callbacks} (expected ~{expectedCallbacks:F0}) {(flowing ? "OK" : "FAIL")}; max input peak {20 * Math.Log10(Math.Max(maxIn, 1e-9)):F1} dBFS; silent for {engine.SilentSeconds:F1} s");
            if (!flowing) failures++;
            engine.Stop();
        }

        // 4. Optional: switch the system default microphone to another input and back (checks the Core Audio calls).
        if (args.Contains("--default-input"))
        {
            var original = CoreAudio.DefaultInputName;
            var other = devices.FirstOrDefault(d => d.HasInput && d.Name != original)?.Name;
            if (original == null || other == null) Console.WriteLine("Default input switch: skipped (needs two inputs)");
            else
            {
                bool set = CoreAudio.SetDefaultInput(other) && CoreAudio.DefaultInputName == other;
                bool restored = CoreAudio.SetDefaultInput(original) && CoreAudio.DefaultInputName == original;
                Console.WriteLine($"Default input switch to '{other}': {(set ? "OK" : "FAIL")}, restore to '{original}': {(restored ? "OK" : "FAIL")}");
                if (!set || !restored) failures++;
            }
        }

        Console.WriteLine(failures == 0 ? "SELFTEST PASSED" : $"SELFTEST FAILED ({failures})");
        return failures == 0 ? 0 : 1;
    }

    /// <summary>Autocorrelation pitch estimate over 60–600 Hz.</summary>
    private static double EstimatePitch(ReadOnlySpan<float> x, int sr)
    {
        int minLag = sr / 600, maxLag = sr / 60;
        double best = 0; int bestLag = minLag;
        for (int lag = minLag; lag <= maxLag; lag++)
        {
            double s = 0;
            for (int i = 0; i + lag < x.Length; i++) s += x[i] * x[i + lag];
            if (s > best) { best = s; bestLag = lag; }
        }
        return sr / (double)bestLag;
    }
}
