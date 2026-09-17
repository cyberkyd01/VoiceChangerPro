using System.Windows;
using VoiceChanger.Core.Logging;
using WinForms = System.Windows.Forms;

namespace VoiceChanger.App.Services;

/// <summary>System tray icon with quick toggles. Uses the WinForms NotifyIcon (stable and lightweight).</summary>
public sealed class TrayService : IDisposable
{
    private readonly App _app;
    private readonly WinForms.NotifyIcon _icon;
    private readonly WinForms.ToolStripMenuItem _masterItem;
    private bool _disposed;

    public TrayService(App app)
    {
        _app = app;
        var menu = new WinForms.ContextMenuStrip();
        var show = new WinForms.ToolStripMenuItem("Open Voice Changer Pro by Cyberkyd", null, (_, _) => _app.ShowMainWindow()) { Font = new System.Drawing.Font(WinForms.Control.DefaultFont, System.Drawing.FontStyle.Bold) };
        _masterItem = new WinForms.ToolStripMenuItem("Voice effects enabled", null, (_, _) => ToggleMaster()) { CheckOnClick = false, Checked = app.Manager.MasterEnabled };
        var exit = new WinForms.ToolStripMenuItem("Exit", null, (_, _) => _app.ExitApplication());
        menu.Items.Add(show);
        menu.Items.Add(_masterItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(exit);

        _icon = new WinForms.NotifyIcon
        {
            Text = "Voice Changer Pro by Cyberkyd",
            ContextMenuStrip = menu,
            Visible = true,
            Icon = LoadIcon()
        };
        _icon.DoubleClick += (_, _) => _app.ShowMainWindow();
        _app.Manager.EnginesChanged += () =>
        {
            try { _app.Dispatcher.BeginInvoke(() => { if (!_disposed) _masterItem.Checked = _app.Manager.MasterEnabled; }); } catch { }
        };
    }

    private static System.Drawing.Icon LoadIcon()
    {
        try
        {
            var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"))?.Stream;
            if (stream != null) return new System.Drawing.Icon(stream, 32, 32);
        }
        catch (Exception ex) { Log.Warn("Tray", "Icon load failed: " + ex.Message); }
        return System.Drawing.SystemIcons.Application;
    }

    private void ToggleMaster()
    {
        _app.Manager.MasterEnabled = !_app.Manager.MasterEnabled;
        _app.MainViewModel.RefreshMaster();
        _masterItem.Checked = _app.Manager.MasterEnabled;
        ShowBalloon("Voice Changer Pro by Cyberkyd", _app.Manager.MasterEnabled ? "Voice effects enabled" : "Voice effects disabled (pass-through)");
    }

    public void ShowBalloon(string title, string message, bool warning = false)
    {
        if (_disposed || !_app.SettingsStore.Settings.ShowNotifications) return;
        try
        {
            _icon.BalloonTipTitle = title;
            _icon.BalloonTipText = message;
            _icon.BalloonTipIcon = warning ? WinForms.ToolTipIcon.Warning : WinForms.ToolTipIcon.Info;
            _icon.ShowBalloonTip(3000);
        }
        catch { /* ignore */ }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _icon.Visible = false;
        _icon.Dispose();
    }
}
