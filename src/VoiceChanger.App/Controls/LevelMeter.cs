using System.Windows;
using System.Windows.Media;

namespace VoiceChanger.App.Controls;

/// <summary>Horizontal dB level meter with green/yellow/red zones and a peak-hold indicator.</summary>
public sealed class LevelMeter : FrameworkElement
{
    public static readonly DependencyProperty LevelProperty = DependencyProperty.Register(nameof(Level), typeof(double), typeof(LevelMeter),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender, OnLevelChanged));
    public static readonly DependencyProperty MinDbProperty = DependencyProperty.Register(nameof(MinDb), typeof(double), typeof(LevelMeter),
        new FrameworkPropertyMetadata(-60.0, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Linear level 0..1 (peak). Converted to dB for display.</summary>
    public double Level { get => (double)GetValue(LevelProperty); set => SetValue(LevelProperty, value); }
    public double MinDb { get => (double)GetValue(MinDbProperty); set => SetValue(MinDbProperty, value); }

    private double _peakHold;
    private DateTime _peakTime;

    private static readonly Brush Background = new SolidColorBrush(Color.FromRgb(0x12, 0x14, 0x19));
    private static readonly Brush Green = new SolidColorBrush(Color.FromRgb(0x3D, 0xD6, 0x8C));
    private static readonly Brush Yellow = new SolidColorBrush(Color.FromRgb(0xF5, 0xC5, 0x23));
    private static readonly Brush Red = new SolidColorBrush(Color.FromRgb(0xF0, 0x47, 0x47));
    private static readonly Pen Border = new(new SolidColorBrush(Color.FromRgb(0x2C, 0x31, 0x3A)), 1);

    static LevelMeter()
    {
        Background.Freeze(); Green.Freeze(); Yellow.Freeze(); Red.Freeze(); Border.Freeze();
    }

    private static void OnLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var m = (LevelMeter)d;
        double v = (double)e.NewValue;
        if (v >= m._peakHold || (DateTime.UtcNow - m._peakTime).TotalMilliseconds > 1200)
        {
            m._peakHold = v;
            m._peakTime = DateTime.UtcNow;
        }
    }

    private double ToFraction(double lin)
    {
        double db = lin <= 1e-6 ? MinDb : 20 * Math.Log10(lin);
        return Math.Clamp((db - MinDb) / -MinDb, 0, 1);
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        var rect = new Rect(0, 0, w, h);
        dc.DrawRoundedRectangle(Background, Border, rect, 3, 3);

        double frac = ToFraction(Level);
        double yellowAt = ToFraction(Math.Pow(10, -12.0 / 20)); // -12 dB
        double redAt = ToFraction(Math.Pow(10, -3.0 / 20));     // -3 dB

        void Seg(double from, double to, Brush b)
        {
            if (to <= from) return;
            dc.DrawRectangle(b, null, new Rect(from * w, 2, (to - from) * w, h - 4));
        }
        Seg(0, Math.Min(frac, yellowAt), Green);
        Seg(yellowAt, Math.Min(frac, redAt), Yellow);
        Seg(redAt, frac, Red);

        double peak = ToFraction(_peakHold);
        if (peak > 0.01)
        {
            var b = peak > redAt ? Red : peak > yellowAt ? Yellow : Green;
            dc.DrawRectangle(b, null, new Rect(Math.Min(peak * w, w - 2), 2, 2, h - 4));
        }
    }
}
