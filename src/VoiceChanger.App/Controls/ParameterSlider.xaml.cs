using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VoiceChanger.App.Controls;

/// <summary>Labelled slider with numeric readout, unit, step and double-click reset to default.</summary>
public partial class ParameterSlider : UserControl
{
    public ParameterSlider()
    {
        InitializeComponent();
        MouseDoubleClick += OnDoubleClick;
        TheSlider.PreviewMouseWheel += OnWheel;
    }

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(ParameterSlider), new PropertyMetadata("Parameter"));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(ParameterSlider), new PropertyMetadata(null));
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(nameof(Unit), typeof(string), typeof(ParameterSlider), new PropertyMetadata("", OnFormatChanged));
    public static readonly DependencyProperty DecimalsProperty = DependencyProperty.Register(nameof(Decimals), typeof(int), typeof(ParameterSlider), new PropertyMetadata(1, OnFormatChanged));
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(ParameterSlider), new PropertyMetadata(0.0));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(ParameterSlider), new PropertyMetadata(1.0));
    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(nameof(Step), typeof(double), typeof(ParameterSlider), new PropertyMetadata(0.1));
    public static readonly DependencyProperty LargeStepProperty = DependencyProperty.Register(nameof(LargeStep), typeof(double), typeof(ParameterSlider), new PropertyMetadata(1.0));
    public static readonly DependencyProperty DefaultValueProperty = DependencyProperty.Register(nameof(DefaultValue), typeof(double), typeof(ParameterSlider), new PropertyMetadata(0.0));
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(ParameterSlider),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnFormatChanged));
    public static readonly DependencyProperty DisplayTextProperty = DependencyProperty.Register(nameof(DisplayText), typeof(string), typeof(ParameterSlider), new PropertyMetadata("0"));
    /// <summary>When true, values are shown as percentages (0..1 → 0..100 %).</summary>
    public static readonly DependencyProperty PercentProperty = DependencyProperty.Register(nameof(Percent), typeof(bool), typeof(ParameterSlider), new PropertyMetadata(false, OnFormatChanged));

    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? Description { get => (string?)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public int Decimals { get => (int)GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double Step { get => (double)GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public double LargeStep { get => (double)GetValue(LargeStepProperty); set => SetValue(LargeStepProperty, value); }
    public double DefaultValue { get => (double)GetValue(DefaultValueProperty); set => SetValue(DefaultValueProperty, value); }
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string DisplayText { get => (string)GetValue(DisplayTextProperty); private set => SetValue(DisplayTextProperty, value); }
    public bool Percent { get => (bool)GetValue(PercentProperty); set => SetValue(PercentProperty, value); }

    private static void OnFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((ParameterSlider)d).UpdateText();

    private void UpdateText()
    {
        double v = Value;
        string text;
        if (Percent) text = $"{v * 100:F0} %";
        else
        {
            string num = v.ToString("F" + Decimals, System.Globalization.CultureInfo.InvariantCulture);
            if (Decimals > 0 && v > 0 && (Unit == "st" || Unit == "dB")) num = "+" + num;
            text = string.IsNullOrEmpty(Unit) ? num : $"{num} {Unit}";
        }
        DisplayText = text;
    }

    private void OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        Value = DefaultValue;
        e.Handled = true;
    }

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        double step = Step * (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1);
        double v = Value + (e.Delta > 0 ? step : -step);
        Value = Math.Clamp(v, Minimum, Maximum);
        e.Handled = true;
    }
}
