using System.Diagnostics;
using System.Windows;
using VoiceChanger.App.ViewModels;

namespace VoiceChanger.App.Views;

public partial class SettingsWindow
{
    /// <summary>Main view model, exposed for the routing section bindings.</summary>
    public MainViewModel Main { get; }

    public SettingsWindow(SettingsViewModel vm, MainViewModel main)
    {
        Main = main;
        InitializeComponent();
        DataContext = vm;
    }

    private void OnSave(object sender, RoutedEventArgs e) => DialogResult = true;
    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnOpenLogs(object sender, RoutedEventArgs e) => Open(((SettingsViewModel)DataContext).LogFolder);
    private void OnOpenPresets(object sender, RoutedEventArgs e) => Open(((SettingsViewModel)DataContext).PresetFolder);

    private static void Open(string folder)
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true }); } catch { }
    }
}
