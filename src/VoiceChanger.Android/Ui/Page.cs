using Android.Views;

namespace VoiceChangerPro.AndroidApp.Ui;

public enum Tab { Live, Voices, Adjust, Record }

/// <summary>One screen of the app. Pages build their views once and refresh them from state.</summary>
public abstract class Page
{
    protected Page(MainActivity host) => Host = host;

    protected MainActivity Host { get; }
    protected AppState State => Host.State;

    public abstract View Root { get; }

    /// <summary>Voice settings changed (any source).</summary>
    public virtual void OnStateChanged(StateChange change) { }

    /// <summary>Live / recording / playback status changed.</summary>
    public virtual void OnAudioStatusChanged() { }

    /// <summary>Headphones were connected or disconnected.</summary>
    public virtual void OnHeadphonesChanged() { }

    /// <summary>Called about 20 times per second while audio is active and the page is visible.</summary>
    public virtual void OnTick() { }

    public virtual void OnShown() { }
    public virtual void OnHidden() { }
}
