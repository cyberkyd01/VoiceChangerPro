using Android.Graphics;
using Android.Views;
using Android.Widget;
using VoiceChangerPro.AndroidApp.Audio;

namespace VoiceChangerPro.AndroidApp.Ui;

/// <summary>Live tab: start/stop, current voice, effect switch, meters and headphone status.</summary>
public sealed class LivePage : Page
{
    private readonly ScrollView _root;
    private readonly TextView _status, _delay, _hint, _bigLabel, _voiceIcon, _voiceName, _pitch, _hpIcon, _hpTitle, _hpText;
    private readonly FrameLayout _bigButton;
    private readonly ImageView _bigIcon;
    private readonly Switch _effect;
    private readonly LevelMeterView _inMeter, _outMeter;
    private readonly View _ring;
    private bool _updatingSwitch;

    public LivePage(MainActivity host) : base(host)
    {
        var c = host;
        var (scroll, body) = UiKit.ScrollPage(c);
        _root = scroll;

        // ── Hero: status + big start/stop button ──
        var hero = UiKit.Card(c, 20);
        hero.SetGravity(GravityFlags.CenterHorizontal);
        var statusRow = UiKit.Row(c);
        _status = UiKit.Text(c, "● Ready", 14, Palette.Muted, bold: true);
        _delay = UiKit.Text(c, "", 13, Palette.Muted);
        statusRow.AddView(_status);
        statusRow.AddView(UiKit.Fill(c));
        statusRow.AddView(_delay);
        hero.AddView(statusRow, UiKit.Lp(UiKit.Match, UiKit.Wrap));

        var ringSize = UiKit.Dp(c, 188);
        var ring = new FrameLayout(c);
        ring.Background = UiKit.Oval(Palette.AccentSoft);
        _ring = ring;
        var ringLp = new LinearLayout.LayoutParams(ringSize, ringSize) { Gravity = GravityFlags.CenterHorizontal, TopMargin = UiKit.Dp(c, 16) };
        hero.AddView(ring, ringLp);

        _bigButton = new FrameLayout(c) { Clickable = true, Focusable = true, ContentDescription = "Start live voice" };
        var buttonSize = UiKit.Dp(c, 156);
        ring.AddView(_bigButton, new FrameLayout.LayoutParams(buttonSize, buttonSize, GravityFlags.Center));
        var inner = UiKit.Column(c);
        inner.SetGravity(GravityFlags.Center);
        _bigIcon = UiKit.Icon(c, Resource.Drawable.ic_nav_live, Color.White, 48);
        _bigLabel = UiKit.Text(c, "START", 18, Color.White, bold: true);
        _bigLabel.LetterSpacing = 0.12f;
        inner.AddView(_bigIcon);
        inner.AddView(_bigLabel, UiKit.Lp(c, UiKit.Wrap, UiKit.Wrap, 0, 6));
        _bigButton.AddView(inner, new FrameLayout.LayoutParams(UiKit.Wrap, UiKit.Wrap, GravityFlags.Center));
        _bigButton.Click += (_, _) => Host.ToggleLive();

        _hint = UiKit.Text(c, "", 14, Palette.Muted);
        _hint.Gravity = GravityFlags.Center;
        hero.AddView(_hint, UiKit.Lp(c, UiKit.Match, UiKit.Wrap, 0, 16));
        body.AddView(hero);

        // ── Current voice ──
        var voiceCard = UiKit.Card(c, 14);
        var voiceRow = UiKit.Row(c);
        _voiceIcon = UiKit.Text(c, "", 26, Palette.Text);
        _voiceIcon.Gravity = GravityFlags.Center;
        _voiceIcon.Background = UiKit.Rounded(c, Palette.Raised, 12);
        voiceRow.AddView(_voiceIcon, new LinearLayout.LayoutParams(UiKit.Dp(c, 48), UiKit.Dp(c, 48)));
        var voiceText = UiKit.Column(c);
        voiceText.AddView(UiKit.Caption(c, "Voice"));
        _voiceName = UiKit.Text(c, "", 16, Palette.Text, bold: true);
        _voiceName.SetSingleLine(true);
        _voiceName.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
        voiceText.AddView(_voiceName);
        voiceRow.AddView(voiceText, UiKit.Lp(c, 0, UiKit.Wrap, 12).WithWeight(1));
        voiceRow.AddView(UiKit.Text(c, "Change ›", 14, Palette.Accent, bold: true));
        voiceCard.AddView(voiceRow);
        voiceCard.Clickable = true;
        voiceCard.Background = UiKit.Pressable(UiKit.Rounded(c, Palette.Card, 16, Palette.Border), UiKit.Rounded(c, Color.White, 16));
        voiceCard.Click += (_, _) => Host.ShowTab(Tab.Voices);
        body.AddView(voiceCard);

        // ── Voice effect master switch ──
        var effectCard = UiKit.Card(c, 14);
        var effectRow = UiKit.Row(c);
        var effectText = UiKit.Column(c);
        effectText.AddView(UiKit.Text(c, "Voice effect", 16, Palette.Text, bold: true));
        effectText.AddView(UiKit.Text(c, "Off = your natural voice (clean-up stays on)", 13, Palette.Muted));
        effectRow.AddView(effectText, UiKit.Lp(0, UiKit.Wrap, 1));
        _effect = new Switch(c);
        UiKit.TintSwitch(_effect);
        _effect.CheckedChange += (_, e) => { if (!_updatingSwitch) State.SetEffectEnabled(e.IsChecked); };
        effectRow.AddView(_effect);
        effectCard.AddView(effectRow);
        body.AddView(effectCard);

        // ── Meters ──
        var meterCard = UiKit.Card(c);
        _inMeter = new LevelMeterView(c);
        _outMeter = new LevelMeterView(c);
        meterCard.AddView(MeterRow(c, "IN", _inMeter));
        meterCard.AddView(MeterRow(c, "OUT", _outMeter), UiKit.Lp(c, UiKit.Match, UiKit.Wrap, 0, 10));
        var pitchRow = UiKit.Row(c);
        pitchRow.AddView(UiKit.Text(c, "Your pitch", 14, Palette.Muted));
        pitchRow.AddView(UiKit.Fill(c));
        _pitch = UiKit.Text(c, "—", 14, Palette.Text, bold: true);
        pitchRow.AddView(_pitch);
        meterCard.AddView(pitchRow, UiKit.Lp(c, UiKit.Match, UiKit.Wrap, 0, 14));
        body.AddView(meterCard);

        // ── Headphones ──
        var hpCard = UiKit.Card(c, 14);
        var hpRow = UiKit.Row(c);
        _hpIcon = UiKit.Text(c, "🎧", 24, Palette.Text);
        hpRow.AddView(_hpIcon);
        var hpText = UiKit.Column(c);
        _hpTitle = UiKit.Text(c, "", 15, Palette.Text, bold: true);
        _hpText = UiKit.Text(c, "", 13, Palette.Muted);
        hpText.AddView(_hpTitle);
        hpText.AddView(_hpText);
        hpRow.AddView(hpText, UiKit.Lp(c, 0, UiKit.Wrap, 12).WithWeight(1));
        hpCard.AddView(hpRow);
        body.AddView(hpCard);

        body.AddView(UiKit.InfoLine(c, "Android does not let apps change the microphone other apps hear. Live mode plays your changed voice in your headphones only. To send a changed voice to WhatsApp, Telegram or any app, use Record."));

        OnStateChanged(StateChange.Preset | StateChange.Effect);
        OnAudioStatusChanged();
        OnHeadphonesChanged();
    }

    public override View Root => _root;

    private static LinearLayout MeterRow(MainActivity c, string label, LevelMeterView meter)
    {
        var row = UiKit.Row(c);
        var l = UiKit.Text(c, label, 12, Palette.Muted, bold: true);
        row.AddView(l, new LinearLayout.LayoutParams(UiKit.Dp(c, 40), UiKit.Wrap));
        row.AddView(meter, new LinearLayout.LayoutParams(0, UiKit.Dp(c, 10), 1));
        return row;
    }

    public override void OnStateChanged(StateChange change)
    {
        _voiceIcon.Text = State.ActivePreset.Icon;
        _voiceName.Text = State.ActivePreset.Name + (State.IsModified ? " (edited)" : "");
        _updatingSwitch = true;
        _effect.Checked = State.EffectEnabled;
        _updatingSwitch = false;
    }

    public override void OnAudioStatusChanged()
    {
        bool live = Host.IsLive;
        var c = Host;
        _bigButton.Background = UiKit.Pressable(UiKit.Oval(live ? Palette.Danger : Palette.Accent), UiKit.Oval(Color.White));
        _ring.Background = UiKit.Oval(live ? Color.ParseColor("#3A2026") : Palette.AccentSoft);
        _bigIcon.SetImageResource(live ? Resource.Drawable.ic_stop : Resource.Drawable.ic_nav_live);
        _bigLabel.Text = live ? "STOP" : "START";
        _bigButton.ContentDescription = live ? "Stop live voice" : "Start live voice";
        _status.Text = live ? "● Live" : "● Ready";
        _status.SetTextColor(live ? Palette.Success : Palette.Muted);
        _hint.Text = live
            ? "You are hearing your changed voice. Switch voices any time."
            : "Tap Start, then speak. You will hear your changed voice in your headphones.";
        if (!live)
        {
            _delay.Text = "";
            _pitch.Text = "—";
            _inMeter.Clear();
            _outMeter.Clear();
        }
    }

    public override void OnHeadphonesChanged()
    {
        var hp = Host.Headphones;
        switch (hp.Kind)
        {
            case HeadphoneKind.Wired:
            case HeadphoneKind.Usb:
                _hpIcon.Text = "🎧";
                _hpTitle.Text = hp.Kind == HeadphoneKind.Wired ? "Wired headphones connected" : "USB headphones connected";
                _hpTitle.SetTextColor(Palette.Success);
                _hpText.Text = "Best choice: lowest delay and no echo.";
                break;
            case HeadphoneKind.Bluetooth:
                _hpIcon.Text = "🎧";
                _hpTitle.Text = "Bluetooth headphones connected";
                _hpTitle.SetTextColor(Palette.Warning);
                _hpText.Text = "Bluetooth adds a noticeable delay. Wired or USB headphones work best.";
                break;
            default:
                _hpIcon.Text = "⚠️";
                _hpTitle.Text = "No headphones connected";
                _hpTitle.SetTextColor(Palette.Warning);
                _hpText.Text = "The phone speaker will feed back into the microphone and howl. Plug in wired or USB headphones.";
                break;
        }
    }

    public override void OnTick()
    {
        var live = Host.Live;
        if (!live.IsRunning) return;
        _inMeter.SetPeak(live.TakeInputPeak());
        _outMeter.SetPeak(live.TakeOutputPeak());
        float hz = live.DetectedPitchHz;
        _pitch.Text = hz > 0 ? $"{hz:F0} Hz" : "—";
        _delay.Text = $"Delay ≈ {live.EstimatedLatencyMs:F0} ms";
    }
}

internal static class LayoutParamsExtensions
{
    public static LinearLayout.LayoutParams WithWeight(this LinearLayout.LayoutParams lp, float weight)
    {
        lp.Weight = weight;
        return lp;
    }
}
