using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using VoiceChanger.Mac.ViewModels;
using VoiceChanger.Mac.Views;

namespace VoiceChanger.Mac;

/// <summary>Renders the vector logo to a PNG (used by build-mac.sh to make the .icns app icon).</summary>
internal static class IconRenderer
{
    public static int Render(string path, int size)
    {
        AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
        var logo = new AppLogo { IconMargin = true, Width = size, Height = size };
        logo.Measure(new Size(size, size));
        logo.Arrange(new Rect(0, 0, size, size));
        using var bmp = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96, 96));
        bmp.Render(logo);
        bmp.Save(path);
        Console.WriteLine($"Icon written: {path} ({size}px)");
        return 0;
    }

    /// <summary>Renders the main window off-screen to a PNG (development aid for checking the layout).</summary>
    public static int Screenshot(string path)
    {
        AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
        var vm = new MainViewModel();
        var window = new MainWindow { DataContext = vm, Width = 1200, Height = 780 };
        window.Show();
        var until = DateTime.UtcNow.AddSeconds(2.5);
        while (DateTime.UtcNow < until) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); }
        var frame = window.CaptureRenderedFrame();
        frame?.Save(path);
        vm.Shutdown();
        window.Close();
        Console.WriteLine(frame != null ? $"Screenshot written: {path}" : "No frame rendered");
        return frame != null ? 0 : 1;
    }
}
