using System.ComponentModel;
using System.Windows;
using VoiceChanger.App.Services;
using VoiceChanger.App.ViewModels;

namespace VoiceChanger.App;

public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
        StateChanged += (_, _) => SaveWindowState();
        SizeChanged += (_, _) => SaveWindowState();
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var s = App.Current.SettingsStore.Settings;
        var work = SystemParameters.WorkArea;
        double w = s.WindowWidth >= MinWidth ? s.WindowWidth : Width;
        double h = s.WindowHeight >= MinHeight ? s.WindowHeight : Height;
        Width = Math.Min(w, work.Width - 16);
        Height = Math.Min(h, work.Height - 16);
        Left = Math.Max(work.Left, work.Left + (work.Width - Width) / 2);
        Top = Math.Max(work.Top, work.Top + (work.Height - Height) / 2);
        if (s.WindowMaximized) WindowState = WindowState.Maximized;
        if (Vm != null) Vm.Dialogs = new DialogService(this);

        // Only a class filter blocks capture outright; merely loaded drivers are logged, not shown.
        var interference = App.Current.Manager.Interference.Where(r => r.IsClassFilter).ToList();
        if (interference.Count > 0 && Vm != null)
        {
            var r = interference[0];
            Vm.InputProblemText = $"{r.Product} ('{r.Component}') is installed as a system-wide microphone filter and will silence every microphone while that product is not running.";
            Vm.HasInputProblem = true;
            Vm.ShowNotification("Another voice changer driver detected", Vm.InputProblemText + " Click 'how to fix' in the status bar.", true);
        }
        else if (s.IsFirstRun && Vm != null)
        {
            Vm.ShowNotification("Welcome to Voice Changer Pro",
                Vm.CableOk
                    ? "Pick a preset, turn on 'Hear myself' with headphones, and click 'Set as Windows default mic' so every app uses your new voice."
                    : "For other apps to hear your changed voice you need a virtual audio cable. Click 'Get VB-CABLE (free)', install it, then press Refresh.");
        }
    }

    private void SaveWindowState()
    {
        if (!IsLoaded) return;
        var s = App.Current.SettingsStore.Settings;
        s.WindowMaximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal)
        {
            s.WindowWidth = Width;
            s.WindowHeight = Height;
        }
        App.Current.SettingsStore.SaveSoon();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        var s = App.Current.SettingsStore.Settings;
        if (s.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
            App.Current.Tray?.ShowBalloon("Still running", "Voice Changer Pro keeps processing in the tray. Right-click the icon to exit.");
        }
        else
        {
            e.Cancel = true;
            App.Current.ExitApplication();
        }
    }
}
