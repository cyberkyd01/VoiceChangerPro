using VoiceChangerPro.AndroidApp.Diagnostics;

namespace VoiceChangerPro.AndroidApp.Audio;

/// <summary>
/// The current recording: the raw take plus a cached copy rendered with the current voice. The cache is reused
/// for Play, Share and Save until the clip or any voice setting changes.
/// </summary>
public sealed class ClipSession
{
    private float[]? _rendered;
    private int _renderedClipId = -1, _renderedVersion = -1;

    public float[]? Raw { get; private set; }
    public int ClipId { get; private set; }
    public bool HasClip => Raw is { Length: > 0 };
    public double Seconds => Raw == null ? 0 : Raw.Length / (double)AudioIo.SampleRate;

    public void SetRaw(float[] raw)
    {
        Raw = raw;
        ClipId++;
        _rendered = null;
    }

    /// <summary>Renders the clip with the current voice on a worker thread (or returns the cached result).</summary>
    public async Task<float[]> RenderAsync(AppState state)
    {
        var raw = Raw ?? throw new InvalidOperationException("No clip recorded");
        if (_rendered != null && _renderedClipId == ClipId && _renderedVersion == state.Version)
            return _rendered;

        // Snapshot the settings on the UI thread; the worker never touches shared state.
        var profile = state.Profile.Clone();
        var global = state.Global;
        int version = state.Version, clipId = ClipId;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await Task.Run(() => OfflineRenderer.Render(raw, profile, global));
        AppLog.Info("Clip", $"Rendered {raw.Length / (double)AudioIo.SampleRate:F1} s with '{state.ActivePreset.Name}' in {sw.ElapsedMilliseconds} ms");
        if (clipId == ClipId)
        {
            _rendered = result;
            _renderedClipId = clipId;
            _renderedVersion = version;
        }
        return result;
    }
}
