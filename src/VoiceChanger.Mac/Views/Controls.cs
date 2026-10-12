using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace VoiceChanger.Mac.Views;

/// <summary>Horizontal level meter (0..1) with green, amber and red zones.</summary>
public sealed class LevelMeter : Control
{
    public static readonly StyledProperty<double> LevelProperty = AvaloniaProperty.Register<LevelMeter, double>(nameof(Level));
    public double Level { get => GetValue(LevelProperty); set => SetValue(LevelProperty, value); }

    static LevelMeter() => AffectsRender<LevelMeter>(LevelProperty);

    private static readonly IBrush Track = new SolidColorBrush(Color.Parse("#2C313A"));
    private static readonly IBrush Green = new SolidColorBrush(Color.Parse("#3DD68C"));
    private static readonly IBrush Amber = new SolidColorBrush(Color.Parse("#F5A623"));
    private static readonly IBrush Red = new SolidColorBrush(Color.Parse("#F04747"));

    public override void Render(DrawingContext ctx)
    {
        var r = new Rect(Bounds.Size);
        ctx.DrawRectangle(Track, null, new RoundedRect(r, 3));
        double v = Math.Clamp(Level, 0, 1);
        if (v <= 0) return;
        var fill = new Rect(0, 0, r.Width * v, r.Height);
        ctx.DrawRectangle(v > 0.92 ? Red : v > 0.75 ? Amber : Green, null, new RoundedRect(fill, 3));
    }
}

/// <summary>A labelled slider with a live value read-out, e.g. "Pitch  +3.0 st".</summary>
public sealed class ParamSlider : UserControl
{
    public static readonly StyledProperty<string> LabelProperty = AvaloniaProperty.Register<ParamSlider, string>(nameof(Label), "");
    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<ParamSlider, double>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<double> MinimumProperty = AvaloniaProperty.Register<ParamSlider, double>(nameof(Minimum));
    public static readonly StyledProperty<double> MaximumProperty = AvaloniaProperty.Register<ParamSlider, double>(nameof(Maximum), 1);
    public static readonly StyledProperty<string> FormatProperty = AvaloniaProperty.Register<ParamSlider, string>(nameof(Format), "{0:0.0}");
    public static readonly StyledProperty<double> ResetValueProperty = AvaloniaProperty.Register<ParamSlider, double>(nameof(ResetValue));

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    /// <summary>Composite format for the read-out; use {0:+0.0;-0.0;0} for signed values, {0:P0} for percentages.</summary>
    public string Format { get => GetValue(FormatProperty); set => SetValue(FormatProperty, value); }
    /// <summary>Value restored by double-clicking the slider (0 for effects, 1 for volumes).</summary>
    public double ResetValue { get => GetValue(ResetValueProperty); set => SetValue(ResetValueProperty, value); }

    private readonly TextBlock _label = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _value = new() { FontSize = 12, Foreground = new SolidColorBrush(Color.Parse("#8A8F98")), HorizontalAlignment = HorizontalAlignment.Right };
    private readonly Slider _slider = new() { Margin = new Thickness(0, -6, 0, -4) };

    public ParamSlider()
    {
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        head.Children.Add(_label);
        Grid.SetColumn(_value, 1);
        head.Children.Add(_value);
        Content = new StackPanel { Spacing = 0, Margin = new Thickness(0, 2, 0, 2), Children = { head, _slider } };
        _slider.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty && Math.Abs(_slider.Value - Value) > 1e-9) Value = _slider.Value;
        };
        _slider.DoubleTapped += (_, _) => Value = Math.Clamp(ResetValue, Minimum, Maximum); // double-click resets
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty) _label.Text = Label;
        else if (change.Property == MinimumProperty) _slider.Minimum = Minimum;
        else if (change.Property == MaximumProperty) _slider.Maximum = Maximum;
        if (change.Property == ValueProperty || change.Property == FormatProperty || change.Property == MinimumProperty || change.Property == MaximumProperty)
        {
            if (Math.Abs(_slider.Value - Value) > 1e-9) _slider.Value = Value;
            _value.Text = string.Format(System.Globalization.CultureInfo.InvariantCulture, Format, Value);
        }
    }
}
