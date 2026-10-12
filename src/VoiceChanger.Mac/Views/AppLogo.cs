using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace VoiceChanger.Mac.Views;

/// <summary>
/// The Voice Changer Pro microphone logo drawn as vectors (same design as the Windows icon), so it is
/// crisp at every size: in the window header and when rendering the macOS app icon.
/// </summary>
public sealed class AppLogo : Control
{
    /// <summary>When true, leaves the transparent margin Apple's icon grid expects (about 10 % per side).</summary>
    public bool IconMargin { get; set; }

    public override void Render(DrawingContext ctx)
    {
        double size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0) return;
        double inset = IconMargin ? size * 0.1 : 0;
        double s = (size - inset * 2) / 256.0; // design units: 256 × 256
        using (ctx.PushTransform(Matrix.CreateScale(s, s) * Matrix.CreateTranslation(inset, inset)))
        {
            var bg = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#5865F2"), 0), new GradientStop(Color.Parse("#2196C1"), 1) },
            };
            ctx.DrawRectangle(bg, null, new RoundedRect(new Rect(0, 0, 256, 256), 52));

            var white = Brushes.White;
            ctx.DrawRectangle(white, null, new RoundedRect(new Rect(93, 38, 70, 115), 35)); // capsule
            var pen = new Pen(white, 16, lineCap: PenLineCap.Round);
            var arc = new StreamGeometry();
            using (var g = arc.Open())
            {
                g.BeginFigure(new Point(64, 132), false);
                g.ArcTo(new Point(192, 132), new Size(64, 64), 0, false, SweepDirection.CounterClockwise);
                g.EndFigure(false);
            }
            ctx.DrawGeometry(null, pen, arc);
            ctx.DrawLine(pen, new Point(128, 198), new Point(128, 214)); // stem
            ctx.DrawLine(pen, new Point(92, 222), new Point(164, 222));  // base
        }
    }
}
