using System.Threading;
using System.Windows;
using System.Windows.Threading;
using VoiceChanger.App.Services;
using VoiceChanger.App.ViewModels;
using VoiceChanger.Core.Audio.Devices;
using VoiceChanger.Core.Audio.Engine;
using VoiceChanger.Core.Logging;
using VoiceChanger.Core.Presets;
using VoiceChanger.Core.Settings;

namespace VoiceChanger.App;

public partial class App : Application
{
    public bool StartMinimized { get; set; }

    public new static App Current => (App)Application.Current;

    public DeviceManager Devices { get; private set; } = null!;
    public SettingsStore SettingsStore { get; private set; } = null!;
    public PresetStore Presets { get; private set; } = null!;
    public EngineManager Manager { get; private set; } = null!;
    public TrayService Tray { get; private set; } = null!;
    public MainViewModel MainViewModel { get; private set; } = null!;
    public MainWindow? MainWin { get; private set; }

    private EventWaitHandle? _showEvent;
    private bool _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log.Error("App", "Unhandled exception", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) => { Log.Error("App", "Unobserved task exception", args.Exception); args.SetObserved(); };

        try
        {
            try
            {
                Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(
                    System.Windows.Media.Color.FromRgb(0x58, 0x65, 0xF2), Wpf.Ui.Appearance.ApplicationTheme.Dark, false);
            }
            catch (Exception ex) { Log.Warn("App", "Accent color could not be applied: " + ex.Message); }

            SettingsStore = new SettingsStore();
            Presets = new PresetStore();
            Devices = new DeviceManager();
            Manager = new EngineManager(Devices, SettingsStore, Presets);
            Manager.Initialize();

            MainViewModel = new MainViewModel(Manager, Presets, SettingsStore, Devices, Dispatcher);
            MainWin = new MainWindow { DataContext = MainViewModel };
            Tray = new TrayService(this);

            bool hide = StartMinimized || (SettingsStore.Settings.StartMinimized && SettingsStore.Settings.MinimizeToTray);
            if (!hide) MainWin.Show();
            else Log.Info("App", "Started minimized to tray");

            StartupService.Sync(SettingsStore.Settings.StartWithWindows);
            ListenForShowRequests();
        }
        catch (Exception ex)
        {
            Log.Error("App", "Fatal startup error", ex);
            MessageBox.Show($"Voice Changer Pro by Cyberkyd could not start:\n\n{ex.Message}\n\nSee the log in {Log.Directory}", "Voice Changer Pro by Cyberkyd", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void ListenForShowRequests()
    {
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Program.ShowEventName);
        var t = new Thread(() =>
        {
            while (!_exiting)
            {
                try
                {
                    if (_showEvent.WaitOne(500)) Dispatcher.BeginInvoke(ShowMainWindow);
                }
                catch { break; }
            }
        }) { IsBackground = true, Name = "ShowRequestListener" };
        t.Start();
    }

    public void ShowMainWindow()
    {
        if (MainWin == null) return;
        MainWin.Show();
        if (MainWin.WindowState == WindowState.Minimized) MainWin.WindowState = WindowState.Normal;
        MainWin.Activate();
        MainWin.Topmost = true; MainWin.Topmost = false;
    }

    public void ExitApplication()
    {
        if (_exiting) return;
        _exiting = true;
        Log.Info("App", "Shutting down");
        try { MainWin?.Hide(); } catch { }
        try { Tray?.Dispose(); } catch { }
        try { Manager?.Shutdown(); } catch (Exception ex) { Log.Error("App", "Shutdown error", ex); }
        try { Manager?.Dispose(); } catch { }
        try { Devices?.Dispose(); } catch { }
        try { SettingsStore?.Dispose(); } catch { }
        Shutdown(0);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (!_exiting)
        {
            _exiting = true;
            try { Tray?.Dispose(); } catch { }
            try { Manager?.Shutdown(); } catch { }
            try { SettingsStore?.Dispose(); } catch { }
        }
        Log.Flush();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("App", "Unhandled UI exception", e.Exception);
        e.Handled = true;
        try
        {
            MainViewModel?.ShowNotification("Unexpected error", e.Exception.Message, isWarning: true);
        }
        catch { /* ignore */ }
    }
}
