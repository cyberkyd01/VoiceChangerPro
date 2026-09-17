using System.Windows;
using VoiceChanger.App.ViewModels;
using VoiceChanger.Core.Presets;

namespace VoiceChanger.App.Views;

public partial class SavePresetDialog
{
    public SavePresetResult? Result { get; private set; }

    public SavePresetDialog(string name, string category, string description, string icon, bool isNew)
    {
        InitializeComponent();
        Title = isNew ? "Save preset" : "Edit preset details";
        NameBox.Text = name;
        foreach (var c in PresetCategories.All) CategoryBox.Items.Add(c);
        CategoryBox.Text = string.IsNullOrWhiteSpace(category) ? PresetCategories.Custom : category;
        DescriptionBox.Text = description;
        IconBox.Text = icon;
        OkButton.Content = isNew ? "Save" : "Update";
        Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (name.Length == 0)
        {
            ErrorText.Text = "Please enter a name.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }
        var category = string.IsNullOrWhiteSpace(CategoryBox.Text) ? PresetCategories.Custom : CategoryBox.Text.Trim();
        Result = new SavePresetResult(name, category, DescriptionBox.Text.Trim(), IconBox.Text.Trim());
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
