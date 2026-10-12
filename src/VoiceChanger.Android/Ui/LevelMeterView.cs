using Android.Content;
using Android.Graphics;
using Android.Views;

namespace VoiceChangerPro.AndroidApp.Ui;

/// <summary>
/// Horizontal peak meter on a -60..0 dB scale: green, yellow above -12 dB, red above -3 dB.
/// Falls back smoothly (about 20 dB per second) so short peaks stay readable.
/// </summary>
public sealed class LevelMeterView : View
{
    private const float FloorDb = -60f;
    private readonly Paint _track = new() { AntiAlias = true };
    private readonly Paint _fill = new() { AntiAlias = true };
    private readonly RectF _rect = new();
    private float _displayDb = FloorDb;
    private long _lastUpdate;

    public LevelMeterView(Context context) : base(context)
    {
        _track.Color = Palette.Border;
    }

    // Activation constructor used if Android ever needs to re-wrap the Java peer.
    private LevelMeterView(IntPtr handle, Android.Runtime.JniHandleOwnership transfer) : base(handle, transfer)
    {
        _track.Color = Palette.Border;
    }

    /// <summary>Feeds a new linear peak (0..1+).</summary>
    public void SetPeak(float linear)
    {
        float db = linear > 1e-6f ? 20f * MathF.Log10(linear) : FloorDb;
        db = Math.Clamp(db, FloorDb, 0f);
        long now = Java.Lang.JavaSystem.CurrentTimeMillis();
        float dt = _lastUpdate == 0 ? 0 : Math.Min(0.5f, (now - _lastUpdate) / 1000f);
        _lastUpdate = now;
        _displayDb = db >= _displayDb ? db : Math.Max(db, _displayDb - 20f * dt);
        Invalidate();
    }

    public void Clear()
    {
        _displayDb = FloorDb;
        _lastUpdate = 0;
        Invalidate();
    }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        float w = Width, h = Height, r = h / 2f;
        _rect.Set(0, 0, w, h);
        canvas.DrawRoundRect(_rect, r, r, _track);

        float frac = (_displayDb - FloorDb) / -FloorDb;
        if (frac <= 0.005f) return;
        _fill.Color = _displayDb > -3f ? Palette.Danger : _displayDb > -12f ? Palette.Warning : Palette.Success;
        _rect.Set(0, 0, Math.Max(h, w * frac), h);
        canvas.DrawRoundRect(_rect, r, r, _fill);
    }
}
