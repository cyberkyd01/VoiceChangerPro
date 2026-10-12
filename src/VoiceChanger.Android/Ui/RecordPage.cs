using Android.Content.Res;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using VoiceChangerPro.AndroidApp.Audio;

namespace VoiceChangerPro.AndroidApp.Ui;

/// <summary>Record tab: record the raw voice, play it back with any voice, share or save the result.</summary>
public sealed class RecordPage : Page
{
    private readonly ScrollView _root;
    private readonly FrameLayout _recButton;
    private readonly View _recDot;
    private readonly ImageView _recStop;
    private readonly TextView _timer, _recHint, _clipLength, _voiceLine, _busyText;
    private readonly LevelMeterView _meter;
    private readonly LinearLayout _clipCard, _busyRow;
    private readonly TextView _play, _playOriginal, _share, _save;
    private readonly ProgressBar _progress;

    public RecordPage(MainActivity host) : base(host)
    {
        var c = host;
        var (scroll, body) = UiKit.ScrollPage(c);
        _root = scroll;

        // ── Recorder ──
        var recCard = UiKit.Card(c, 20);
        recCard.SetGravity(GravityFlags.CenterHorizontal);
        var ringSize = UiKit.Dp(c, 128);
        var ring = new FrameLayout(c);
        ring.Background = UiKit.Oval(Color.Transparent, Palette.Border, UiKit.Dp(c, 4));
        recCard.AddView(ring, new LinearLayout.LayoutParams(ringSize, ringSize) { Gravity = GravityFlags.CenterHorizontal });
        _recButton = new FrameLayout(c) { Clickable = true, Focusable = true, ContentDescription = "Record" };
        _recButton.Background = UiKit.Pressable(UiKit.Oval(Palette.Card), UiKit.Oval(Color.White));
        ring.AddView(_recButton, new FrameLayout.LayoutParams(ringSize - UiKit.Dp(c, 16), ringSize - UiKit.Dp(c, 16), GravityFlags.Center));
        _recDot = new View(c) { Background = UiKit.Oval(Palette.Danger) };
        _recButton.AddView(_recDot, new FrameLayout.LayoutParams(UiKit.Dp(c, 64), UiKit.Dp(c, 64), GravityFlags.Center));
        _recStop = UiKit.Icon(c, Resource.Drawable.ic_stop, Palette.Danger, 52);
        _recButton.AddView(_recStop, new FrameLayout.LayoutParams(UiKit.Dp(c, 52), UiKit.Dp(c, 52), GravityFlags.Center));
        _recButton.Click += (_, _) => Host.ToggleRecording();

        _timer = UiKit.Text(c, "00:00", 34, Palette.Text, bold: true);
        _timer.Gravity = GravityFlags.Center;
        _timer.SetTypeface(Typeface.Monospace, TypefaceStyle.Bold);
        recCard.AddView(_timer, UiKit.Lp(c, UiKit.Match, UiKit.Wrap, 0, 14));
        _meter = new LevelMeterView(c);
        recCard.AddView(_meter, UiKit.Lp(c, UiKit.Match, UiKit.Dp(c, 8), 24, 10, 24, 0));
        _recHint = UiKit.Text(c, "", 14, Palette.Muted);
        _recHint.Gravity = GravityFlags.Center;
        recCard.AddView(_recHint, UiKit.Lp(c, UiKit.Match, UiKit.Wrap, 0, 12));
        body.AddView(recCard);

        // ── Clip actions ──
        _clipCard = UiKit.Card(c);
        var head = UiKit.Row(c);
        head.AddView(UiKit.Caption(c, "Your recording"), UiKit.Lp(0, UiKit.Wrap, 1));
        _clipLength = UiKit.Text(c, "", 14, Palette.Muted, bold: true);
        head.AddView(_clipLength);
        _clipCard.AddView(head);

        _voiceLine = UiKit.Text(c, "", 15, Palette.Text);
        _voiceLine.SetSingleLine(true);
        _voiceLine.Ellipsize = Android.Text.TextUtils.TruncateAt.End;
        var voiceRow = UiKit.Row(c);
        voiceRow.AddView(_voiceLine, UiKit.Lp(0, UiKit.Wrap, 1));
        var change = UiKit.Text(c, "Change ›", 14, Palette.Accent, bold: true);
        change.SetPadding(UiKit.Dp(c, 12), UiKit.Dp(c, 10), 0, UiKit.Dp(c, 10));
        change.Clickable = true;
        change.Click += (_, _) => Host.ShowTab(Tab.Voices);
        voiceRow.AddView(change);
        _clipCard.AddView(voiceRow, UiKit.Lp(c, UiKit.Match, UiKit.Wrap, 0, 6));

        _play = UiKit.Button(c, "▶  Play with voice", ButtonStyle.Primary);
        _play.Click += (_, _) => { if (Host.IsPlaying) Host.StopPlayback(); else Host.PlayClip(withVoice: true); };
        _clipCard.AddView(_play, UiKit.Lp(c, UiKit.Match, UiKit.Wrap, 0, 8));
        _playOriginal = UiKit.Button(c, "Play original", ButtonStyle.Secondary);
        _playOriginal.Click += (_, _) => Host.PlayClip(withVoice: false);
        _clipCard.AddView(_playOriginal, UiKit.Lp(c, UiKit.Match, UiKit.Wrap, 0, 8));

        _progress = new ProgressBar(c, null, Android.Resource.Attribute.ProgressBarStyleHorizontal) { Max = 1000 };
        _progress.ProgressTintList = ColorStateList.ValueOf(Palette.Accent);
        _progress.ProgressBackgroundTintList = ColorStateList.ValueOf(Palette.Border);
        _clipCard.AddView(_progress, UiKit.Lp(c, UiKit.Match, UiKit.Dp(c, 6), 0, 12));

        _busyRow = UiKit.Row(c);
        var spinner = new ProgressBar(c, null, Android.Resource.Attribute.ProgressBarStyleSmall) { Indeterminate = true };
        spinner.IndeterminateTintList = ColorStateList.ValueOf(Palette.Accent);
        _busyRow.AddView(spinner, new LinearLayout.LayoutParams(UiKit.Dp(c, 24), UiKit.Dp(c, 24)));
        _busyText = UiKit.Text(c, "Applying the voice…", 14, Palette.Muted);
        _busyRow.AddView(_busyText, UiKit.Lp(c, UiKit.Wrap, UiKit.Wrap, 10));
        _clipCard.AddView(_busyRow, UiKit.Lp(c, UiKit.Match, UiKit.Wrap, 0, 10));

        var shareRow = UiKit.Row(c);
        _share = UiKit.Button(c, "Share", ButtonStyle.Secondary);
        _share.Click += (_, _) => Host.ShareClip();
        _save = UiKit.Button(c, "Save", ButtonStyle.Secondary);
        _save.Click += (_, _) => Host.SaveClip();
        shareRow.AddView(_share, UiKit.Lp(c, 0, UiKit.Wrap, 0, 0, 6).WithWeight(1));
        shareRow.AddView(_save, UiKit.Lp(c, 0, UiKit.Wrap, 6).WithWeight(1));
        _clipCard.AddView(shareRow, UiKit.Lp(c, UiKit.Match, UiKit.Wrap, 0, 12));
        body.AddView(_clipCard);

        body.AddView(UiKit.InfoLine(c, "To use a changed voice in WhatsApp, Telegram or other apps: record here, pick a voice, then Share. Save puts a copy in Music › Voice Changer Pro."));

        OnStateChanged(StateChange.Preset);
        OnAudioStatusChanged();
    }

    public override View Root => _root;

    public override void OnStateChanged(StateChange change)
    {
        _voiceLine.Text = $"Voice: {State.ActivePreset.Icon} {State.ActivePreset.Name}" + (State.IsModified ? " (edited)" : "");
    }

    public override void OnAudioStatusChanged()
    {
        bool recording = Host.IsRecording;
        bool hasClip = Host.Clip.HasClip;
        bool busy = Host.IsRendering;
        bool playing = Host.IsPlaying;

        _recDot.Visibility = recording ? ViewStates.Gone : ViewStates.Visible;
        _recStop.Visibility = recording ? ViewStates.Visible : ViewStates.Gone;
        _recButton.ContentDescription = recording ? "Stop recording" : "Record";
        _meter.Visibility = recording ? ViewStates.Visible : ViewStates.Invisible;
        if (!recording) _meter.Clear();

        if (recording)
        {
            _recHint.Text = "Recording… tap to stop.";
        }
        else if (hasClip)
        {
            _timer.Text = Format(Host.Clip.Seconds);
            _recHint.Text = "Tap the button to record a new take.";
        }
        else
        {
            _timer.Text = "00:00";
            _recHint.Text = $"Tap to record up to {ClipRecorder.MaxSeconds} seconds.";
        }

        _clipLength.Text = hasClip ? $"{Host.Clip.Seconds:F1} s" : "";
        bool canUse = hasClip && !recording && !busy;
        UiKit.SetEnabled(_play, canUse || playing);
        UiKit.SetEnabled(_playOriginal, canUse && !playing);
        UiKit.SetEnabled(_share, canUse);
        UiKit.SetEnabled(_save, canUse);
        _play.Text = playing ? "■  Stop" : "▶  Play with voice";
        UiKit.Style(_play, playing ? ButtonStyle.Danger : ButtonStyle.Primary);
        _busyRow.Visibility = busy ? ViewStates.Visible : ViewStates.Gone;
        _busyText.Text = Host.BusyText;
        _progress.Visibility = playing ? ViewStates.Visible : ViewStates.Gone;
        if (!playing) _progress.Progress = 0;
        _clipCard.Alpha = hasClip || recording ? 1f : 0.6f;
    }

    public override void OnTick()
    {
        if (Host.IsRecording)
        {
            _timer.Text = Format(Host.Recorder.Seconds);
            _meter.SetPeak(Host.Recorder.TakePeak());
        }
        if (Host.IsPlaying)
            _progress.Progress = (int)(Host.Player.Progress * 1000);
    }

    private static string Format(double seconds)
    {
        int s = (int)Math.Floor(seconds);
        return $"{s / 60:00}:{s % 60:00}";
    }
}
