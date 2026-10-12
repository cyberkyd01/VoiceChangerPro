using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Text;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace VoiceChangerPro.AndroidApp.Ui;

/// <summary>Colours of the Windows app (dark theme).</summary>
public static class Palette
{
    public static readonly Color Background = Color.ParseColor("#171A20");
    public static readonly Color Nav = Color.ParseColor("#13161B");
    public static readonly Color Card = Color.ParseColor("#1E2229");
    public static readonly Color Raised = Color.ParseColor("#262B34");
    public static readonly Color Border = Color.ParseColor("#2C313A");
    public static readonly Color Accent = Color.ParseColor("#5865F2");
    public static readonly Color AccentSoft = Color.ParseColor("#2A2F5A");
    public static readonly Color Success = Color.ParseColor("#3DD68C");
    public static readonly Color Warning = Color.ParseColor("#F5A623");
    public static readonly Color Danger = Color.ParseColor("#F04747");
    public static readonly Color Muted = Color.ParseColor("#8A8F98");
    public static readonly Color Text = Color.ParseColor("#E9ECF1");
    public static readonly Color Ripple = Color.ParseColor("#33FFFFFF");
}

public enum ButtonStyle { Primary, Secondary, Danger, Ghost }

/// <summary>Small factory for the app's views so every screen shares one look. All UI is built in code.</summary>
public static class UiKit
{
    public const int Match = ViewGroup.LayoutParams.MatchParent;
    public const int Wrap = ViewGroup.LayoutParams.WrapContent;

    public static int Dp(Context c, float dp) => (int)Math.Round(dp * c.Resources!.DisplayMetrics!.Density);

    public static LinearLayout.LayoutParams Lp(int width, int height, float weight = 0) => new(width, height, weight);

    public static LinearLayout.LayoutParams Lp(Context c, int width, int height, float left = 0, float top = 0, float right = 0, float bottom = 0)
    {
        var lp = new LinearLayout.LayoutParams(width, height);
        lp.SetMargins(Dp(c, left), Dp(c, top), Dp(c, right), Dp(c, bottom));
        return lp;
    }

    public static GradientDrawable Rounded(Context c, Color fill, float radiusDp, Color? stroke = null, float strokeDp = 1)
    {
        var d = new GradientDrawable();
        d.SetShape(ShapeType.Rectangle);
        d.SetCornerRadius(Dp(c, radiusDp));
        d.SetColor(fill);
        if (stroke is Color s) d.SetStroke(Math.Max(1, Dp(c, strokeDp)), s);
        return d;
    }

    public static GradientDrawable Oval(Color fill, Color? stroke = null, int strokePx = 0)
    {
        var d = new GradientDrawable();
        d.SetShape(ShapeType.Oval);
        d.SetColor(fill);
        if (stroke is Color s) d.SetStroke(strokePx, s);
        return d;
    }

    /// <summary>Wraps a background in a touch ripple.</summary>
    public static Drawable Pressable(Drawable content, Drawable? mask = null) =>
        new RippleDrawable(ColorStateList.ValueOf(Palette.Ripple), content, mask ?? content);

    public static TextView Text(Context c, string text, float sp, Color color, bool bold = false)
    {
        var t = new TextView(c) { Text = text };
        t.SetTextSize(ComplexUnitType.Sp, sp);
        t.SetTextColor(color);
        if (bold) t.SetTypeface(Typeface.Create("sans-serif-medium", TypefaceStyle.Normal), TypefaceStyle.Normal);
        return t;
    }

    /// <summary>Upper-case section caption.</summary>
    public static TextView Caption(Context c, string text)
    {
        var t = Text(c, text.ToUpperInvariant(), 12, Palette.Muted, bold: true);
        t.LetterSpacing = 0.08f;
        return t;
    }

    public static LinearLayout Column(Context c) => new(c) { Orientation = Android.Widget.Orientation.Vertical };

    public static LinearLayout Row(Context c)
    {
        var r = new LinearLayout(c) { Orientation = Android.Widget.Orientation.Horizontal };
        r.SetGravity(GravityFlags.CenterVertical);
        return r;
    }

    /// <summary>Rounded card with the standard border and padding.</summary>
    public static LinearLayout Card(Context c, float paddingDp = 16)
    {
        var card = Column(c);
        card.Background = Rounded(c, Palette.Card, 16, Palette.Border);
        int p = Dp(c, paddingDp);
        card.SetPadding(p, p, p, p);
        card.LayoutParameters = Lp(c, Match, Wrap, 0, 0, 0, 12);
        return card;
    }

    public static TextView Button(Context c, string text, ButtonStyle style)
    {
        var b = Text(c, text, 15, Color.White, bold: true);
        b.Gravity = GravityFlags.Center;
        b.SetMinHeight(Dp(c, 48));
        b.SetPadding(Dp(c, 16), Dp(c, 10), Dp(c, 16), Dp(c, 10));
        b.Clickable = true;
        b.Focusable = true;
        b.SetSingleLine(true);
        b.Ellipsize = TextUtils.TruncateAt.End;
        Style(b, style);
        return b;
    }

    public static void Style(TextView b, ButtonStyle style)
    {
        var c = b.Context!;
        Drawable bg = style switch
        {
            ButtonStyle.Primary => Rounded(c, Palette.Accent, 12),
            ButtonStyle.Danger => Rounded(c, Palette.Danger, 12),
            ButtonStyle.Secondary => Rounded(c, Palette.Raised, 12, Palette.Border),
            _ => Rounded(c, Color.Transparent, 12),
        };
        b.Background = Pressable(bg, Rounded(c, Color.White, 12));
        b.SetTextColor(style == ButtonStyle.Ghost ? Palette.Accent : Color.White);
    }

    public static void SetEnabled(View v, bool enabled)
    {
        v.Enabled = enabled;
        v.Alpha = enabled ? 1f : 0.4f;
    }

    public static ImageView Icon(Context c, int drawableId, Color tint, float sizeDp)
    {
        var iv = new ImageView(c);
        iv.SetImageResource(drawableId);
        iv.ImageTintList = ColorStateList.ValueOf(tint);
        iv.LayoutParameters = new LinearLayout.LayoutParams(Dp(c, sizeDp), Dp(c, sizeDp));
        return iv;
    }

    /// <summary>A one-line hint with an info glyph, used for the platform limitation notes.</summary>
    public static TextView InfoLine(Context c, string text)
    {
        var t = Text(c, "ⓘ  " + text, 13, Palette.Muted);
        t.SetLineSpacing(Dp(c, 2), 1f);
        t.LayoutParameters = Lp(c, Match, Wrap, 4, 4, 4, 16);
        return t;
    }

    public static void TintSeekBar(SeekBar s)
    {
        s.ProgressTintList = ColorStateList.ValueOf(Palette.Accent);
        s.ThumbTintList = ColorStateList.ValueOf(Palette.Accent);
        s.ProgressBackgroundTintList = ColorStateList.ValueOf(Palette.Border);
    }

    public static void TintSwitch(Switch s)
    {
        var states = new[] { new[] { Android.Resource.Attribute.StateChecked }, Array.Empty<int>() };
        s.ThumbTintList = new ColorStateList(states, new int[] { Palette.Accent, Color.ParseColor("#C9CDD4") });
        s.TrackTintList = new ColorStateList(states, new int[] { Color.ParseColor("#7F5865F2"), Palette.Border });
    }

    public static Space Spacer(Context c, float dp, bool vertical = true)
    {
        var s = new Space(c);
        s.LayoutParameters = vertical ? new LinearLayout.LayoutParams(1, Dp(c, dp)) : new LinearLayout.LayoutParams(Dp(c, dp), 1);
        return s;
    }

    public static View Fill(Context c)
    {
        var v = new View(c);
        v.LayoutParameters = new LinearLayout.LayoutParams(0, 1, 1f);
        return v;
    }

    /// <summary>A vertically scrolling page body with standard side padding.</summary>
    public static (ScrollView scroll, LinearLayout body) ScrollPage(Context c)
    {
        var scroll = new ScrollView(c) { FillViewport = true };
        scroll.LayoutParameters = new ViewGroup.LayoutParams(Match, Match);
        scroll.OverScrollMode = OverScrollMode.IfContentScrolls;
        var body = Column(c);
        int p = Dp(c, 16);
        body.SetPadding(p, Dp(c, 8), p, p);
        scroll.AddView(body, new ViewGroup.LayoutParams(Match, Wrap));
        return (scroll, body);
    }
}
