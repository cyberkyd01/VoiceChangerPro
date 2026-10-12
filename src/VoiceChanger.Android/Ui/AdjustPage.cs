using Android.Graphics;
using Android.Views;
using Android.Widget;

namespace VoiceChangerPro.AndroidApp.Ui;

/// <summary>Adjust tab: pitch, formant and volume on top of the chosen voice, plus global clean-up.</summary>
public sealed class AdjustPage : Page
{
    private readonly ScrollView _root;
    private readonly TextView _voiceIcon, _voiceName, _modified;
    private readonly TextView _reset;
    private readonly Slider _pitch, _formant, _volume, _noise, _isolation;

    public AdjustPage(MainActivity host) : base(host)
    {
        var c = host;
        var (scroll, body) = UiKit.ScrollPage(c);
        _root = scroll;

        // ── Current voice + reset ──
        var voiceCard = UiKit.Card(c);
        var voiceRow = UiKit.Row(c);
        _voiceIcon = UiKit.Text(c, "", 24, Palette.Text);
        _voiceIcon.Gravity = GravityFlags.Center;
        _voiceIcon.Background = UiKit.Rounded(c, Palette.Raised, 12);
        voiceRow.AddView(_voiceIcon, new LinearLayout.LayoutParams(UiKit.Dp(c, 46), UiKit.Dp(c, 46)));
        var voiceText = UiKit.Column(c);
        voiceText.AddView(UiKit.Caption(c, "Adjusting"));
        _voiceName = UiKit.Text(c, "", 16, Palette.Text, bold: true);
        _voiceName.SetSingleLine(true);
        _voiceName.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
        voiceText.AddView(_voiceName);
        voiceRow.AddView(voiceText, UiKit.Lp(c, 0, UiKit.Wrap, 12).WithWeight(1));
        _modified = UiKit.Text(c, "EDITED", 11, Palette.Warning, bold: true);
        _modified.Background = UiKit.Rounded(c, Color.ParseColor("#33F5A623"), 8);
        _modified.SetPadding(UiKit.Dp(c, 8), UiKit.Dp(c, 3), UiKit.Dp(c, 8), UiKit.Dp(c, 3));
        voiceRow.AddView(_modified);
        voiceCard.AddView(voiceRow);
        _reset = UiKit.Button(c, "Reset to preset", ButtonStyle.Secondary);
        _reset.Click += (_, _) => State.ResetToPreset();
        voiceCard.AddView(_reset, UiKit.Lp(c, UiKit.Match, UiKit.Wrap, 0, 14));
        body.AddView(voiceCard);

        // ── Voice ──
        var voice = UiKit.Card(c);
        voice.AddView(UiKit.Caption(c, "Voice"));
        _pitch = new Slider(c, "Pitch", "Higher or lower voice, in semitones (12 = one octave).", -12, 12, 0.1f,
            v => $"{v:+0.0;-0.0;0.0} st", v => State.SetPitch(v));
        _formant = new Slider(c, "Formant", "Size of the throat and mouth. Plus sounds smaller (female, child), minus sounds bigger.", -6, 6, 0.1f,
            v => $"{v:+0.0;-0.0;0.0} st", v => State.SetFormant(v));
        _volume = new Slider(c, "Output volume", "Loudness of the changed voice.", -12, 12, 0.5f,
            v => $"{v:+0.0;-0.0;0.0} dB", v => State.SetOutputGain(v));
        voice.AddView(_pitch.View);
        voice.AddView(_formant.View);
        voice.AddView(_volume.View);
        body.AddView(voice);

        // ── Clean-up ──
        var clean = UiKit.Card(c);
        clean.AddView(UiKit.Caption(c, "Clean-up · all voices"));
        _noise = new Slider(c, "Noise reduction", "Removes steady background noise such as fans, hiss and traffic.", 0, 100, 1,
            v => $"{v:F0} %", v => State.SetNoiseReduction(v / 100f));
        _isolation = new Slider(c, "Voice isolation", "Turns down other people and sounds around you.", 0, 100, 1,
            v => $"{v:F0} %", v => State.SetVoiceIsolation(v / 100f));
        clean.AddView(_noise.View);
        clean.AddView(_isolation.View);
        body.AddView(clean);

        body.AddView(UiKit.InfoLine(c, "Changes are heard right away in Live mode and are used when you play or share a recording."));

        OnStateChanged(StateChange.Preset | StateChange.Profile | StateChange.Global);
    }

    public override View Root => _root;

    public override void OnStateChanged(StateChange change)
    {
        _voiceIcon.Text = State.ActivePreset.Icon;
        _voiceName.Text = State.ActivePreset.Name;
        bool modified = State.IsModified;
        _modified.Visibility = modified ? ViewStates.Visible : ViewStates.Gone;
        UiKit.SetEnabled(_reset, modified);
        _pitch.SetValue(State.Profile.PitchSemitones);
        _formant.SetValue(State.Profile.FormantSemitones);
        _volume.SetValue(State.Profile.OutputGainDb);
        _noise.SetValue(State.NoiseReduction * 100f);
        _isolation.SetValue(State.VoiceIsolation * 100f);
    }

    /// <summary>Labelled SeekBar with a value read-out, mapping a float range onto integer steps.</summary>
    private sealed class Slider
    {
        private readonly float _min, _step;
        private readonly Func<float, string> _format;
        private readonly TextView _value;
        private readonly SeekBar _bar;
        private bool _updating;

        public Slider(MainActivity c, string label, string hint, float min, float max, float step, Func<float, string> format, Action<float> changed)
        {
            _min = min;
            _step = step;
            _format = format;
            View = UiKit.Column(c);
            View.SetPadding(0, UiKit.Dp(c, 14), 0, 0);

            var head = UiKit.Row(c);
            head.AddView(UiKit.Text(c, label, 15, Palette.Text, bold: true), UiKit.Lp(0, UiKit.Wrap, 1));
            _value = UiKit.Text(c, "", 15, Palette.Accent, bold: true);
            head.AddView(_value);
            View.AddView(head);

            _bar = new SeekBar(c) { Max = (int)Math.Round((max - min) / step) };
            UiKit.TintSeekBar(_bar);
            _bar.SetPadding(UiKit.Dp(c, 4), UiKit.Dp(c, 10), UiKit.Dp(c, 4), UiKit.Dp(c, 10));
            _bar.ProgressChanged += (_, e) =>
            {
                if (_updating || !e.FromUser) return;
                float v = _min + e.Progress * _step;
                _value.Text = _format(v);
                changed(v);
            };
            View.AddView(_bar, UiKit.Lp(UiKit.Match, UiKit.Wrap));

            var hintView = UiKit.Text(c, hint, 12, Palette.Muted);
            View.AddView(hintView);
        }

        public LinearLayout View { get; }

        public void SetValue(float v)
        {
            _value.Text = _format(v);
            int progress = (int)Math.Round((v - _min) / _step);
            if (_bar.Progress == progress) return;
            _updating = true;
            _bar.Progress = Math.Clamp(progress, 0, _bar.Max);
            _updating = false;
        }
    }
}
