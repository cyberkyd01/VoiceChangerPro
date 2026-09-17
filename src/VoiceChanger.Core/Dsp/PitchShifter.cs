namespace VoiceChanger.Core.Dsp;

/// <summary>
/// Real-time time-domain pitch shifter: WSOLA time-stretching followed by fractional
/// resampling (transposition) that restores the original duration. Output sample count always
/// equals input sample count; a fixed internal latency keeps the stream continuous.
/// Time-domain processing keeps voices natural (no phase-vocoder smearing) and is cheap.
/// Note: like all time-domain shifters it scales formants along with pitch; the
/// <see cref="FormantShifter"/> stage compensates for that afterwards.
/// </summary>
public sealed class PitchShifter
{
    private readonly float _fs;
    private readonly int _seq, _seek, _ovl;
    private readonly float[] _mid;
    private readonly FloatFifo _input = new(16384);
    private readonly FloatFifo _stretched = new(16384);
    private readonly FloatFifo _output = new(16384);
    private readonly int _latency;

    // transposer state
    private double _readPos;

    private double _ratio = 1.0;
    private double _tempo = 1.0;
    private double _skipFract;
    private bool _bypass = true;
    private readonly DelayLine _bypassDelay;
    private readonly float[] _ovlWinUp, _ovlWinDown;

    public int LatencySamples => _latency;
    public int Underruns { get; private set; }

    public PitchShifter(float sampleRate)
    {
        _fs = sampleRate;
        _seq = (int)(sampleRate * 0.032f);   // 32 ms analysis sequence
        _seek = (int)(sampleRate * 0.012f);  // 12 ms search range
        _ovl = (int)(sampleRate * 0.008f);   // 8 ms crossfade
        _mid = new float[_ovl];
        _latency = _seq + _seek + 256;
        _bypassDelay = new DelayLine(_latency + 8);
        _ovlWinUp = new float[_ovl];
        _ovlWinDown = new float[_ovl];
        for (int i = 0; i < _ovl; i++)
        {
            float t = (i + 0.5f) / _ovl;
            _ovlWinUp[i] = t;
            _ovlWinDown[i] = 1f - t;
        }
        Reset();
    }

    /// <summary>Pitch ratio (2.0 = one octave up). Values within 0.2 % of 1.0 bypass processing.</summary>
    public void SetRatio(double ratio)
    {
        ratio = Math.Clamp(ratio, 0.25, 4.0);
        bool bypass = Math.Abs(ratio - 1.0) < 0.002;
        if (bypass != _bypass)
        {
            _bypass = bypass;
            // switching modes: clear the other path so no stale audio appears
            if (_bypass) _bypassDelay.Clear();
            else ResetStretcher();
        }
        _ratio = ratio;
        _tempo = 1.0 / ratio;
    }

    public void Reset()
    {
        _bypassDelay.Clear();
        ResetStretcher();
    }

    private void ResetStretcher()
    {
        _input.Clear();
        _stretched.Clear();
        _output.Clear();
        _output.WriteZeros(_latency);
        Array.Clear(_mid);
        _readPos = 0;
        _skipFract = 0;
    }

    public void Process(Span<float> buf)
    {
        if (_bypass)
        {
            for (int i = 0; i < buf.Length; i++)
            {
                float x = buf[i];
                buf[i] = _bypassDelay.Read(_latency);
                _bypassDelay.Write(x);
            }
            return;
        }

        _input.Write(buf);
        Stretch();
        Transpose();

        int got = _output.Read(buf);
        if (got < buf.Length)
        {
            Underruns++;
            buf.Slice(got).Clear();
        }
        // Keep latency bounded: if the output backlog grows because of ratio changes, trim it.
        int maxBacklog = _latency * 2 + buf.Length;
        if (_output.Count > maxBacklog) _output.Discard(_output.Count - _latency);
    }

    /// <summary>WSOLA: overlap-add sequences at the best-correlated positions to change tempo without changing pitch.</summary>
    private void Stretch()
    {
        int required = _seq + _seek;
        Span<float> ola = stackalloc float[_ovl];
        while (_input.Count >= required)
        {
            var src = _input.Buffer;
            int off = _input.Offset;
            int best = SeekBestOffset(src, off);
            int start = off + best;

            // crossfade previous tail with the new sequence head
            for (int i = 0; i < _ovl; i++)
                ola[i] = _mid[i] * _ovlWinDown[i] + src[start + i] * _ovlWinUp[i];
            _stretched.Write(ola);

            // straight copy of the middle section
            int copyLen = _seq - 2 * _ovl;
            _stretched.Write(src.AsSpan(start + _ovl, copyLen));

            // remember tail for the next crossfade
            Array.Copy(src, start + _seq - _ovl, _mid, 0, _ovl);

            // advance input by nominal skip (tempo-scaled), accumulating fraction
            _skipFract += _tempo * (_seq - _ovl);
            int skip = (int)_skipFract;
            _skipFract -= skip;
            _input.Consume(skip);
        }
    }

    /// <summary>Reads the stretched FIFO at fractional steps of <c>ratio</c> (Hermite interpolation).</summary>
    private void Transpose()
    {
        int avail = _stretched.Count;
        if (avail < 4) return;
        var src = _stretched.Buffer;
        int off = _stretched.Offset;
        double pos = _readPos;
        int produced = 0;
        Span<float> tmp = stackalloc float[512];
        while (true)
        {
            int idx = (int)pos;
            if (idx + 3 >= avail) break;
            float t = (float)(pos - idx);
            float v = DspUtil.Hermite(src[off + idx], src[off + idx + 1], src[off + idx + 2], src[off + idx + 3], t);
            tmp[produced++] = v;
            if (produced == tmp.Length)
            {
                _output.Write(tmp);
                produced = 0;
            }
            pos += _ratio;
        }
        if (produced > 0) _output.Write(tmp.Slice(0, produced));
        int consumed = (int)pos;
        _stretched.Consume(consumed);
        _readPos = pos - consumed;
    }

    private int SeekBestOffset(float[] src, int off)
    {
        // Coarse search (step 4) then refine around the best coarse candidate.
        double bestCorr = double.NegativeInfinity;
        int best = 0;
        for (int o = 0; o < _seek; o += 4)
        {
            double c = Correlate(src, off + o);
            if (c > bestCorr) { bestCorr = c; best = o; }
        }
        int lo = Math.Max(0, best - 3), hi = Math.Min(_seek - 1, best + 3);
        for (int o = lo; o <= hi; o++)
        {
            if (o == best) continue;
            double c = Correlate(src, off + o);
            if (c > bestCorr) { bestCorr = c; best = o; }
        }
        return best;
    }

    private double Correlate(float[] src, int start)
    {
        double corr = 0, norm = 1e-9;
        for (int i = 0; i < _ovl; i++)
        {
            float s = src[start + i];
            corr += s * _mid[i];
            norm += s * s;
        }
        return corr / Math.Sqrt(norm);
    }
}
