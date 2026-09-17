using System.Windows;
using Microsoft.Win32;
using VoiceChanger.App.ViewModels;
using VoiceChanger.App.Views;

namespace VoiceChanger.App.Services;

public sealed class DialogService : IDialogService
{
    private readonly Window _owner;

    public DialogService(Window owner) => _owner = owner;

    public SavePresetResult? ShowSavePreset(string name, string category, string description, string icon, bool isNew)
    {
        var dlg = new SavePresetDialog(name, category, description, icon, isNew) { Owner = _owner };
        return dlg.ShowDialog() == true ? dlg.Result : null;
    }

    public bool Confirm(string title, string message)
        => MessageBox.Show(_owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public void ShowMessage(string title, string message)
        => MessageBox.Show(_owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public string? OpenFile(string filter)
    {
        var d = new OpenFileDialog { Filter = filter, CheckFileExists = true };
        return d.ShowDialog(_owner) == true ? d.FileName : null;
    }

    public string? SaveFile(string filter, string defaultName)
    {
        var d = new SaveFileDialog { Filter = filter, FileName = defaultName, AddExtension = true };
        return d.ShowDialog(_owner) == true ? d.FileName : null;
    }

    public void ShowSettings()
    {
        var app = App.Current;
        var vm = new SettingsViewModel(app.SettingsStore, app.Presets, app.Manager);
        var win = new SettingsWindow(vm, app.MainViewModel) { Owner = _owner };
        if (win.ShowDialog() == true)
        {
            bool bufferChanged = vm.Apply();
            app.MainViewModel.OnSettingsChanged(bufferChanged);
        }
    }
}
