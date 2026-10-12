using Avalonia.Data.Converters;
using Avalonia.Media;

namespace VoiceChanger.Mac.Views;

public static class Converters
{
    private static readonly IBrush Green = new SolidColorBrush(Color.Parse("#2EA66E"));
    private static readonly IBrush Grey = new SolidColorBrush(Color.Parse("#4A505C"));
    private static readonly IBrush OkText = new SolidColorBrush(Color.Parse("#3DD68C"));
    private static readonly IBrush MutedText = new SolidColorBrush(Color.Parse("#8A8F98"));

    public static readonly IValueConverter RunningBrush = new FuncValueConverter<bool, IBrush>(r => r ? Green : Grey);
    public static readonly IValueConverter StartStopText = new FuncValueConverter<bool, string>(r => r ? "Stop" : "Start");
    public static readonly IValueConverter OkBrush = new FuncValueConverter<bool, IBrush>(ok => ok ? OkText : MutedText);
}
