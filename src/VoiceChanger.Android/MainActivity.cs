using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Media;
using Android.OS;
using Android.Views;
using Android.Widget;
using VoiceChangerPro.AndroidApp.Audio;
using VoiceChangerPro.AndroidApp.Diagnostics;
using VoiceChangerPro.AndroidApp.Sharing;
using VoiceChangerPro.AndroidApp.Ui;

namespace VoiceChangerPro.AndroidApp;

/// <summary>
/// The single screen of the app: a top bar, four tabs (Live, Voices, Adjust, Record) and a bottom navigation bar.
/// Owns the audio objects and makes sure only one of them uses the microphone or speaker at a time.
///
/// Test-only launch extras (adb shell am start -n com.cyberkyd.voicechangerpro/.MainActivity ...):
///   --ez selftest true    run the DSP self-test and log the results
///   --ez synthetic true   use a synthetic voice instead of the microphone (for emulators)
///   --ez verbose true     log audio statistics every 5 seconds
///   --es tab record       open a tab (live, voices, adjust, record)
/// </summary>
[Activity(
    Label = "@string/app_name",
    MainLauncher = true,
    Exported = true,
    Name = "com.cyberkyd.voicechangerpro.MainActivity",
    ScreenOrientation = ScreenOrientation.Portrait,
    LaunchMode = LaunchMode.SingleTop,
    WindowSoftInputMode = SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout |
                           ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden |
                           ConfigChanges.Navigation | ConfigChanges.UiMode | ConfigChanges.Density)]
public sealed class MainActivity : Activity
{
    private const int RequestMicForLive = 101, RequestMicForRecord = 102, RequestStorageForSave = 103;

    private readonly Handler _handler = new(Looper.MainLooper!);
    private readonly Dictionary<Tab, Page> _pages = new();
    private readonly Dictionary<Tab, (ImageView Icon, TextView Label)> _nav = new();
    private AudioManager? _audio;
    private NoisyReceiver? _noisyReceiver;
    private DeviceCallback? _deviceCallback;
    private BackCallback? _backCallback;
    private bool _backRegistered;
    private bool _ticking, _started;
    private bool _synthetic;
    private Action? _pendingMicAction;
    private Tab _tab = Tab.Live;
    private TextView _statusPill = null!, _testPill = null!;

    public AppState State { get; private set; } = null!;
    public LiveVoiceEngine Live { get; private set; } = null!;
    public ClipRecorder Recorder { get; private set; } = null!;
    public ClipPlayer Player { get; private set; } = null!;
    public ClipSession Clip { get; } = new();
    public HeadphoneStatus Headphones { get; private set; }
    public bool IsRendering { get; private set; }
    public string BusyText { get; private set; } = "";

    public bool IsLive => Live.IsRunning;
    public bool IsRecording => Recorder.IsRecording;
    public bool IsPlaying => Player.IsPlaying;

    // ───────────────────────────── Lifecycle ─────────────────────────────

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (_, e) => AppLog.Error("Crash", "Unhandled exception", e.Exception);

        SetupWindow();
        _audio = (AudioManager?)GetSystemService(AudioService);
        State = new AppState(this);
        State.Changed += OnStateChanged;

        Live = new LiveVoiceEngine(AudioIo.FramesPerBurst(this));
        Live.Failed += message => RunOnUiThread(() =>
        {
            StopLive();
            ShowMessage("Live voice stopped", message);
        });
        Recorder = new ClipRecorder();
        Recorder.Failed += message => RunOnUiThread(() =>
        {
            FinishRecording();
            ShowMessage("Recording stopped", message);
        });
        Player = new ClipPlayer();
        Headphones = OutputRoutes.Detect(_audio);

        BuildUi();

        _deviceCallback = new DeviceCallback(() => RunOnUiThread(RefreshHeadphones));
        _audio?.RegisterAudioDeviceCallback(_deviceCallback, _handler);

        AppLog.Info("App", $"Started v{PackageManager?.GetPackageInfo(PackageName!, 0)?.VersionName} on Android {Build.VERSION.Release} " +
                           $"(API {(int)Build.VERSION.SdkInt}), {Build.Manufacturer} {Build.Model}, burst={AudioIo.FramesPerBurst(this)}");
        HandleLaunchExtras(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        HandleLaunchExtras(intent);
    }

    protected override void OnStart()
    {
        base.OnStart();
        _started = true;
        _noisyReceiver ??= new NoisyReceiver(OnBecomingNoisy);
        var filter = new IntentFilter(AudioManager.ActionAudioBecomingNoisy);
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            RegisterReceiver(_noisyReceiver, filter, ReceiverFlags.NotExported);
        else
            RegisterReceiver(_noisyReceiver, filter);
        RefreshHeadphones();
    }

    protected override void OnStop()
    {
        // Simplest robust rule: audio only runs while the app is on screen.
        _started = false;
        bool wasLive = IsLive;
        StopEverything();
        if (wasLive) AppLog.Info("App", "Live voice stopped because the app left the screen");
        try { if (_noisyReceiver != null) UnregisterReceiver(_noisyReceiver); } catch { /* not registered */ }
        base.OnStop();
    }

    protected override void OnDestroy()
    {
        State.Changed -= OnStateChanged;
        _handler.RemoveCallbacksAndMessages(null);
        Live.Dispose();
        Recorder.Dispose();
        Player.Dispose();
        if (_deviceCallback != null) _audio?.UnregisterAudioDeviceCallback(_deviceCallback);
        base.OnDestroy();
    }

    private void SetupWindow()
    {
        var window = Window!;
        // Draw behind the system bars on every version; ApplyInsets pads the content.
#pragma warning disable CA1422
        if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            window.SetDecorFitsSystemWindows(false);
        else
            window.DecorView.SystemUiFlags = SystemUiFlags.LayoutStable | SystemUiFlags.LayoutFullscreen | SystemUiFlags.LayoutHideNavigation;
        window.SetStatusBarColor(Color.Transparent);
        window.SetNavigationBarColor(Palette.Nav);
#pragma warning restore CA1422
    }

    // ───────────────────────────── UI shell ─────────────────────────────

    private void BuildUi()
    {
        var root = UiKit.Column(this);
        root.SetBackgroundColor(Palette.Background);

        // Top bar
        var top = UiKit.Row(this);
        top.SetPadding(UiKit.Dp(this, 16), UiKit.Dp(this, 10), UiKit.Dp(this, 16), UiKit.Dp(this, 10));
        var logo = new ImageView(this);
        logo.SetImageResource(Resource.Mipmap.ic_launcher);
        top.AddView(logo, new LinearLayout.LayoutParams(UiKit.Dp(this, 36), UiKit.Dp(this, 36)));
        var titles = UiKit.Column(this);
        titles.AddView(UiKit.Text(this, "Voice Changer Pro", 19, Palette.Text, bold: true));
        titles.AddView(UiKit.Text(this, "by Cyberkyd", 12, Palette.Muted));
        titles.LongClickable = true;
        titles.LongClick += (_, _) => RunSelfTest();
        top.AddView(titles, UiKit.Lp(this, 0, UiKit.Wrap, 12).WithWeight(1));
        _testPill = Pill("TEST INPUT", Palette.Warning);
        _testPill.Visibility = ViewStates.Gone;
        top.AddView(_testPill, UiKit.Lp(this, UiKit.Wrap, UiKit.Wrap, 0, 0, 6));
        _statusPill = Pill("● LIVE", Palette.Danger);
        _statusPill.Visibility = ViewStates.Gone;
        top.AddView(_statusPill);
        root.AddView(top, UiKit.Lp(UiKit.Match, UiKit.Wrap));

        // Pages
        var content = new FrameLayout(this);
        _pages[Tab.Live] = new LivePage(this);
        _pages[Tab.Voices] = new VoicesPage(this);
        _pages[Tab.Adjust] = new AdjustPage(this);
        _pages[Tab.Record] = new RecordPage(this);
        foreach (var page in _pages.Values)
            content.AddView(page.Root, new FrameLayout.LayoutParams(UiKit.Match, UiKit.Match));
        root.AddView(content, UiKit.Lp(UiKit.Match, 0, 1));

        // Bottom navigation
        var navContainer = UiKit.Column(this);
        navContainer.SetBackgroundColor(Palette.Nav);
        var divider = new View(this);
        divider.SetBackgroundColor(Palette.Border);
        navContainer.AddView(divider, UiKit.Lp(UiKit.Match, Math.Max(1, UiKit.Dp(this, 1))));
        var nav = UiKit.Row(this);
        AddNavItem(nav, Tab.Live, "Live", Resource.Drawable.ic_nav_live);
        AddNavItem(nav, Tab.Voices, "Voices", Resource.Drawable.ic_nav_voices);
        AddNavItem(nav, Tab.Adjust, "Adjust", Resource.Drawable.ic_nav_adjust);
        AddNavItem(nav, Tab.Record, "Record", Resource.Drawable.ic_nav_record);
        navContainer.AddView(nav, UiKit.Lp(UiKit.Match, UiKit.Wrap));
        root.AddView(navContainer, UiKit.Lp(UiKit.Match, UiKit.Wrap));

        SetContentView(root);
        ApplyInsets(root, navContainer);
        ShowTab(Tab.Live);
    }

    private TextView Pill(string text, Color color)
    {
        var pill = UiKit.Text(this, text, 11, color, bold: true);
        pill.LetterSpacing = 0.08f;
        pill.Background = UiKit.Rounded(this, Color.Argb(0x30, color.R, color.G, color.B), 10);
        pill.SetPadding(UiKit.Dp(this, 10), UiKit.Dp(this, 4), UiKit.Dp(this, 10), UiKit.Dp(this, 4));
        return pill;
    }

    private void AddNavItem(LinearLayout nav, Tab tab, string label, int icon)
    {
        var item = UiKit.Column(this);
        item.SetGravity(GravityFlags.Center);
        item.SetPadding(0, UiKit.Dp(this, 8), 0, UiKit.Dp(this, 8));
        item.Clickable = true;
        item.Focusable = true;
        item.ContentDescription = label;
        item.Background = new RippleDrawable(Android.Content.Res.ColorStateList.ValueOf(Palette.Ripple), null, null);
        var iv = UiKit.Icon(this, icon, Palette.Muted, 24);
        var tv = UiKit.Text(this, label, 12, Palette.Muted, bold: true);
        item.AddView(iv);
        item.AddView(tv, UiKit.Lp(this, UiKit.Wrap, UiKit.Wrap, 0, 3));
        item.Click += (_, _) => ShowTab(tab);
        nav.AddView(item, UiKit.Lp(0, UiKit.Wrap, 1));
        _nav[tab] = (iv, tv);
    }

    /// <summary>Pads the content for the status bar, the navigation bar and the on-screen keyboard.</summary>
    private void ApplyInsets(View root, View navContainer)
    {
        root.SetOnApplyWindowInsetsListener(new InsetsListener((v, insets) =>
        {
            int top, left, right, bottom, ime;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                var bars = insets.GetInsets(WindowInsets.Type.SystemBars() | WindowInsets.Type.DisplayCutout());
                top = bars.Top; left = bars.Left; right = bars.Right; bottom = bars.Bottom;
                ime = insets.GetInsets(WindowInsets.Type.Ime()).Bottom;
            }
            else
            {
#pragma warning disable CA1422
                top = insets.SystemWindowInsetTop; left = insets.SystemWindowInsetLeft; right = insets.SystemWindowInsetRight;
                bottom = insets.SystemWindowInsetBottom;
#pragma warning restore CA1422
                // Before Android 11 the keyboard height arrives as part of the bottom inset.
                ime = bottom > UiKit.Dp(this, 120) ? bottom : 0;
                if (ime > 0) bottom = 0;
            }
            bool keyboard = ime > bottom;
            v.SetPadding(left, top, right, keyboard ? ime : 0);
            navContainer.SetPadding(0, 0, 0, keyboard ? 0 : bottom);
            navContainer.Visibility = keyboard ? ViewStates.Gone : ViewStates.Visible;
            return Build.VERSION.SdkInt >= BuildVersionCodes.R ? WindowInsets.Consumed! : insets;
        }));
        root.RequestApplyInsets();
    }

    public void ShowTab(Tab tab)
    {
        if (_tab != tab && _pages.TryGetValue(_tab, out var old)) old.OnHidden();
        _tab = tab;
        foreach (var (t, page) in _pages)
            page.Root.Visibility = t == tab ? ViewStates.Visible : ViewStates.Gone;
        foreach (var (t, (icon, label)) in _nav)
        {
            var color = t == tab ? Palette.Accent : Palette.Muted;
            icon.ImageTintList = Android.Content.Res.ColorStateList.ValueOf(color);
            label.SetTextColor(t == tab ? Palette.Text : Palette.Muted);
        }
        _pages[tab].OnShown();
        UpdateBackHandling();
    }

    // Back from another tab returns to Live; back on Live leaves the app.
    private void UpdateBackHandling()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu) return;
        _backCallback ??= new BackCallback(() => ShowTab(Tab.Live));
        if (_tab != Tab.Live && !_backRegistered)
        {
            OnBackInvokedDispatcher.RegisterOnBackInvokedCallback(global::Android.Window.IOnBackInvokedDispatcher.PriorityDefault, _backCallback);
            _backRegistered = true;
        }
        else if (_tab == Tab.Live && _backRegistered)
        {
            OnBackInvokedDispatcher.UnregisterOnBackInvokedCallback(_backCallback);
            _backRegistered = false;
        }
    }

#pragma warning disable CS0672, CA1422
    public override void OnBackPressed()
    {
        if (_tab != Tab.Live) ShowTab(Tab.Live);
        else base.OnBackPressed();
    }
#pragma warning restore CS0672, CA1422

    // ───────────────────────────── State & status ─────────────────────────────

    private void OnStateChanged(StateChange change)
    {
        if (IsLive)
        {
            if (change.HasFlag(StateChange.Profile) || change.HasFlag(StateChange.Preset)) Live.Apply(State.Profile);
            if (change.HasFlag(StateChange.Global)) Live.ApplyGlobal(State.Global);
            if (change.HasFlag(StateChange.Effect)) Live.SetBypass(!State.EffectEnabled);
        }
        foreach (var page in _pages.Values) page.OnStateChanged(change);
    }

    private void AudioStatusChanged()
    {
        bool active = IsLive || IsRecording;
        _statusPill.Text = IsRecording ? "● REC" : "● LIVE";
        _statusPill.Visibility = active ? ViewStates.Visible : ViewStates.Gone;
        if (active) Window!.AddFlags(WindowManagerFlags.KeepScreenOn);
        else Window!.ClearFlags(WindowManagerFlags.KeepScreenOn);
        foreach (var page in _pages.Values) page.OnAudioStatusChanged();
        if (IsLive || IsRecording || IsPlaying) EnsureTicking();
    }

    private void EnsureTicking()
    {
        if (_ticking) return;
        _ticking = true;
        _handler.PostDelayed(Tick, 50);
    }

    private void Tick()
    {
        if (Recorder.IsRecording && Recorder.ReachedLimit)
        {
            FinishRecording();
            ShowToast($"Reached the {ClipRecorder.MaxSeconds} second limit.");
        }
        if (Player.Finished)
        {
            Player.Stop();
            AudioStatusChanged();
        }
        if (!(IsLive || IsRecording || IsPlaying))
        {
            _ticking = false;
            return;
        }
        _pages[_tab].OnTick();
        _handler.PostDelayed(Tick, 50);
    }

    private void RefreshHeadphones()
    {
        var hp = OutputRoutes.Detect(_audio);
        if (hp == Headphones) return;
        Headphones = hp;
        AppLog.Info("Routes", $"Output is now {(hp.Connected ? $"{hp.Kind} headphones ({hp.Name})" : "the phone speaker")}");
        foreach (var page in _pages.Values) page.OnHeadphonesChanged();
        // Backup for devices that do not send "becoming noisy" for USB headsets.
        if (IsLive && !Live.SpeakerSafetyVolume && !hp.Connected) OnBecomingNoisy();
    }

    private void OnBecomingNoisy()
    {
        if (IsLive)
        {
            StopLive();
            ShowToast("Headphones disconnected. Live voice stopped to prevent feedback.");
        }
        if (IsPlaying)
        {
            Player.Stop();
            AudioStatusChanged();
        }
    }

    private void StopEverything()
    {
        StopLive();
        FinishRecording();
        if (IsPlaying) Player.Stop();
        AudioStatusChanged();
    }

    // ───────────────────────────── Live ─────────────────────────────

    public void ToggleLive()
    {
        if (IsLive) { StopLive(); return; }
        WithMicPermission(RequestMicForLive, ConfirmOutputThenStartLive);
    }

    private void ConfirmOutputThenStartLive()
    {
        RefreshHeadphones();
        if (Headphones.Connected)
        {
            StartLive(speaker: false);
            return;
        }
        new AlertDialog.Builder(this)
            .SetTitle("No headphones connected")!
            .SetMessage("Without headphones your changed voice comes out of the phone speaker and goes straight back into the microphone. That causes loud howling (feedback).\n\n" +
                        "Plug in wired or USB headphones for the best result. Bluetooth headphones work too, but add delay.\n\n" +
                        "Start anyway at reduced volume?")!
            .SetPositiveButton("Start anyway", (_, _) => StartLive(speaker: true))!
            .SetNegativeButton("Cancel", (_, _) => { })!
            .Show();
    }

    private void StartLive(bool speaker)
    {
        if (IsLive || !_started) return;
        FinishRecording();
        if (IsPlaying) Player.Stop();
        Live.SyntheticInput = _synthetic;
        Live.SpeakerSafetyVolume = speaker;
        try
        {
            Live.Start(State.Profile, State.Global, !State.EffectEnabled);
            if (Headphones.Kind == HeadphoneKind.Bluetooth)
                ShowToast("Bluetooth adds delay. Wired or USB headphones are faster.");
        }
        catch (AudioUnavailableException ex)
        {
            ShowMessage("Could not start live voice", ex.Message);
        }
        catch (Exception ex)
        {
            AppLog.Error("Live", "Start failed", ex);
            ShowMessage("Could not start live voice", "The microphone could not be started. Please try again.");
        }
        AudioStatusChanged();
    }

    public void StopLive()
    {
        if (!IsLive) return;
        Live.Stop();
        AudioStatusChanged();
    }

    // ───────────────────────────── Record & share ─────────────────────────────

    public void ToggleRecording()
    {
        if (IsRecording) { FinishRecording(); return; }
        WithMicPermission(RequestMicForRecord, StartRecording);
    }

    private void StartRecording()
    {
        if (IsRecording || IsRendering || !_started) return;
        StopLive();
        if (IsPlaying) Player.Stop();
        Recorder.SyntheticInput = _synthetic;
        try
        {
            Recorder.Start();
        }
        catch (AudioUnavailableException ex)
        {
            ShowMessage("Could not record", ex.Message);
        }
        catch (Exception ex)
        {
            AppLog.Error("Record", "Start failed", ex);
            ShowMessage("Could not record", "The microphone could not be started. Please try again.");
        }
        AudioStatusChanged();
    }

    private void FinishRecording()
    {
        if (!IsRecording) return;
        var clip = Recorder.Stop();
        if (clip.Length < AudioIo.SampleRate / 2)
            ShowToast("That recording was too short. Hold on a little longer.");
        else
            Clip.SetRaw(clip);
        AudioStatusChanged();
    }

    public async void PlayClip(bool withVoice)
    {
        if (!Clip.HasClip || IsRendering || IsRecording) return;
        StopLive();
        if (IsPlaying) Player.Stop();
        float[]? samples = withVoice ? await RenderClipAsync("Applying the voice…") : Clip.Raw;
        if (samples == null || !_started) return;
        try
        {
            Player.Play(samples);
        }
        catch (Exception ex)
        {
            AppLog.Error("Player", "Play failed", ex);
            ShowMessage("Could not play", ex is AudioUnavailableException ? ex.Message : "Playback could not start.");
        }
        AudioStatusChanged();
    }

    public void StopPlayback()
    {
        Player.Stop();
        AudioStatusChanged();
    }

    private async Task<float[]?> RenderClipAsync(string busyText)
    {
        IsRendering = true;
        BusyText = busyText;
        AudioStatusChanged();
        try
        {
            return await Clip.RenderAsync(State);
        }
        catch (Exception ex)
        {
            AppLog.Error("Clip", "Render failed", ex);
            ShowMessage("Something went wrong", "The voice could not be applied to this recording.");
            return null;
        }
        finally
        {
            IsRendering = false;
            AudioStatusChanged();
        }
    }

    public async void ShareClip()
    {
        if (!Clip.HasClip || IsRendering || IsRecording) return;
        if (IsPlaying) Player.Stop();
        var rendered = await RenderClipAsync("Preparing your clip…");
        if (rendered == null) return;
        try
        {
            var name = ClipExporter.FileNameFor(State.ActivePreset.Name);
            await Task.Run(() => ClipExporter.WriteShareCopy(this, rendered, name));
            ClipExporter.Share(this, name);
        }
        catch (ActivityNotFoundException)
        {
            ShowMessage("Nothing to share with", "No app on this phone can receive audio files.");
        }
        catch (Exception ex)
        {
            AppLog.Error("Export", "Share failed", ex);
            ShowMessage("Could not share", "The clip could not be prepared for sharing.");
        }
    }

    public void SaveClip()
    {
        if (!Clip.HasClip || IsRendering || IsRecording) return;
        if (ClipExporter.NeedsStoragePermission && CheckSelfPermission(Manifest.Permission.WriteExternalStorage) != Permission.Granted)
        {
            RequestPermissions(new[] { Manifest.Permission.WriteExternalStorage }, RequestStorageForSave);
            return;
        }
        SaveRenderedClip();
    }

    private async void SaveRenderedClip()
    {
        if (IsPlaying) Player.Stop();
        var rendered = await RenderClipAsync("Saving your clip…");
        if (rendered == null) return;
        try
        {
            var name = ClipExporter.FileNameFor(State.ActivePreset.Name);
            var location = await Task.Run(() => ClipExporter.SaveToMusic(this, rendered, name));
            ShowMessage("Saved", $"Your clip is in {location}");
        }
        catch (Exception ex)
        {
            AppLog.Error("Export", "Save failed", ex);
            ShowMessage("Could not save", "The clip could not be saved to the Music folder. You can still use Share.");
        }
    }

    // ───────────────────────────── Permissions ─────────────────────────────

    private void WithMicPermission(int requestCode, Action action)
    {
        if (CheckSelfPermission(Manifest.Permission.RecordAudio) == Permission.Granted)
        {
            action();
            return;
        }
        _pendingMicAction = action;
        RequestPermissions(new[] { Manifest.Permission.RecordAudio }, requestCode);
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        bool granted = grantResults.Length > 0 && grantResults[0] == Permission.Granted;
        switch (requestCode)
        {
            case RequestMicForLive:
            case RequestMicForRecord:
                var action = _pendingMicAction;
                _pendingMicAction = null;
                if (granted) action?.Invoke();
                else ExplainMicrophoneDenied();
                break;
            case RequestStorageForSave:
                if (granted) SaveRenderedClip();
                else ShowMessage("Cannot save", "Saving to the Music folder needs storage access on this Android version. You can still use Share.");
                break;
        }
    }

    private void ExplainMicrophoneDenied()
    {
        // When Android will no longer show the request, only the system settings can grant it.
        bool blocked = !ShouldShowRequestPermissionRationale(Manifest.Permission.RecordAudio);
        var builder = new AlertDialog.Builder(this)
            .SetTitle("Microphone needed")!
            .SetMessage("Voice Changer Pro needs the microphone to hear and change your voice. " +
                        "Your voice is processed on this phone and never uploaded." +
                        (blocked ? "\n\nOpen Settings › Permissions › Microphone and choose Allow." : ""))!;
        if (blocked)
        {
            builder.SetPositiveButton("Open settings", (_, _) => OpenAppSettings());
            builder.SetNegativeButton("Not now", (_, _) => { });
        }
        else
        {
            builder.SetPositiveButton("OK", (_, _) => { });
        }
        builder.Show();
    }

    private void OpenAppSettings()
    {
        try
        {
            var intent = new Intent(Android.Provider.Settings.ActionApplicationDetailsSettings,
                Android.Net.Uri.FromParts("package", PackageName, null));
            StartActivity(intent);
        }
        catch (Exception ex)
        {
            AppLog.Warn("App", $"Could not open settings: {ex.Message}");
        }
    }

    // ───────────────────────────── Helpers ─────────────────────────────

    public void ShowToast(string message)
    {
        if (IsFinishing || IsDestroyed) return;
        Toast.MakeText(this, message, ToastLength.Short)?.Show();
    }

    public void ShowMessage(string title, string message)
    {
        if (IsFinishing || IsDestroyed) return;
        new AlertDialog.Builder(this)
            .SetTitle(title)!
            .SetMessage(message)!
            .SetPositiveButton("OK", (_, _) => { })!
            .Show();
    }

    private void RunSelfTest()
    {
        ShowToast("Running the voice engine self-test…");
        Task.Run(DspSelfTest.Run).ContinueWith(t => RunOnUiThread(() =>
        {
            if (t.IsFaulted)
            {
                AppLog.Error("SelfTest", "Self-test crashed", t.Exception?.GetBaseException());
                ShowMessage("Voice engine self-test", "The self-test could not run: " + t.Exception?.GetBaseException().Message);
            }
            else
            {
                ShowMessage("Voice engine self-test", DspSelfTest.Format(t.Result));
            }
        }));
    }

    private void HandleLaunchExtras(Intent? intent)
    {
        if (intent == null) return;
        if (intent.GetBooleanExtra("verbose", false)) AppLog.Verbose = true;
        if (intent.GetBooleanExtra("synthetic", false))
        {
            _synthetic = true;
            _testPill.Visibility = ViewStates.Visible;
            AppLog.Info("Test", "Synthetic input enabled: live and record use a generated voice instead of the microphone");
        }
        var tab = intent.GetStringExtra("tab");
        if (!string.IsNullOrEmpty(tab) && Enum.TryParse<Tab>(tab, ignoreCase: true, out var t)) ShowTab(t);
        if (intent.GetBooleanExtra("selftest", false)) RunSelfTest();
    }

    // ───────────────────────────── Platform callbacks ─────────────────────────────

    private sealed class NoisyReceiver(Action onNoisy) : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent?.Action == AudioManager.ActionAudioBecomingNoisy) onNoisy();
        }
    }

    private sealed class DeviceCallback(Action onChanged) : AudioDeviceCallback
    {
        public override void OnAudioDevicesAdded(AudioDeviceInfo[]? addedDevices) => onChanged();
        public override void OnAudioDevicesRemoved(AudioDeviceInfo[]? removedDevices) => onChanged();
    }

    private sealed class BackCallback(Action onBack) : Java.Lang.Object, global::Android.Window.IOnBackInvokedCallback
    {
        public void OnBackInvoked() => onBack();
    }

    private sealed class InsetsListener(Func<View, WindowInsets, WindowInsets> apply) : Java.Lang.Object, View.IOnApplyWindowInsetsListener
    {
        public WindowInsets OnApplyWindowInsets(View v, WindowInsets insets) => apply(v, insets);
    }
}
