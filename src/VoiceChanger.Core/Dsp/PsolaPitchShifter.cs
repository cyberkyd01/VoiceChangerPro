namespace VoiceChanger.Core.Dsp;

/// <summary>
/// Real-time TD-PSOLA (time-domain pitch-synchronous overlap-add) pitch shifter.
///
/// A pitch tracker (zero-padded FFT autocorrelation, window-bias corrected, sub-harmonic guarded,
/// median smoothed, with voicing hysteresis) runs every 5 ms. Pitch marks are chained one period apart
/// and refined by waveform similarity: each new mark is placed where the local waveform best matches the
/// previous mark's grain, which keeps consecutive grains phase-coherent and removes the jitter that
/// makes naive peak-picked PSOLA sound rough or "throaty". Synthesis places output marks at
/// period / ratio and overlap-adds two-period Hann grains from the nearest input mark. Grains are whole
/// pitch periods and are never resampled, so formants are preserved exactly. Unvoiced sound passes
/// through with fixed 5 ms grains, unchanged. Output sample count always equals input sample count.
/// </summary>
public sealed class PsolaPitchShifter
{
    private readonly float _fs;
    private readonly int _n, _fftN, _hop, _minP, _maxP, _unvoicedP, _latency;
    private readonly Fft _fft;
    private readonly double[] _window, _windowAcf, _re, _im;
    private readonly int _mask;
    private readonly float[] _x, _ola, _olaW;   // rings indexed by absolute sample index
    private readonly DelayLine _bypassDelay;

    private long _avail;
    private long _analysisCenter;
    private double _nextMark;
    private double _nextOut;
    private long _outRead;

    private readonly (long center, float period, bool voiced)[] _frames = new (long, float, bool)[64];
    private int _frameCount;
    private readonly (long pos, float period, bool voiced)[] _marks = new (long, float, bool)[512];
    private int _markCount;
    private readonly float[] _recentPeriods = new float[3];
    private int _recentIdx;
    private bool _trackVoiced;
    private float _lastVoicedPeriod;

    private double _ratio = 1.0;
    private bool _bypass = true;

    public int LatencySamples => _latency;
    public float DetectedPitchHz { get; private set; }

    public PsolaPitchShifter(float sampleRate)
    {
        _fs = sampleRate;
        _n = sampleRate > 64000 ? 2048 : 1024;
        _fftN = _n * 2;
        _fft = new Fft(_fftN);
        _hop = Math.Max(64, (int)(sampleRate * 0.005f));
        _minP = (int)(sampleRate / 400);
        _maxP = (int)(sampleRate / 70);
        _unvoicedP = Math.Max(32, (int)(sampleRate * 0.005f));
        _latency = _n / 2 + (int)(2.5 * _maxP) + 64;

        int size = 1;
        while (size < _latency * 4 + _fftN) size <<= 1;
        _mask = size - 1;
        _x = new float[size]; _ola = new float[size]; _olaW = new float[size];

        _window = new double[_n];
        for (int i = 0; i < _n; i++) _window[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / _n);
        _re = new double[_fftN]; _im = new double[_fftN];
        _windowAcf = new double[_n];
        Array.Clear(_re); Array.Clear(_im);
        for (int i = 0; i < _n; i++) _re[i] = _window[i];
        _fft.Forward(_re, _im);
        for (int k = 0; k < _fftN; k++) { _re[k] = _re[k] * _re[k] + _im[k] * _im[k]; _im[k] = 0; }
        _fft.Inverse(_re, _im);
        for (int i = 0; i < _n; i++) _windowAcf[i] = Math.Max(_re[i] / _re[0], 0.05);

        _bypassDelay = new DelayLine(_latency + 8);
        Reset();
    }

    public void SetRatio(double ratio)
    {
        ratio = Math.Clamp(ratio, 0.25, 4.0);
        bool bypass = Math.Abs(ratio - 1.0) < 0.002;
        if (bypass != _bypass)
        {
            _bypass = bypass;
            if (_bypass) _bypassDelay.Clear(); else ResetPsola();
        }
        _ratio = ratio;
    }

    public void Reset()
    {
        _bypassDelay.Clear();
        ResetPsola();
    }

    private void ResetPsola()
    {
        Array.Clear(_x); Array.Clear(_ola); Array.Clear(_olaW);
        _avail = 0;
        _analysisCenter = _n / 2;
        _nextMark = 0;
        _nextOut = 0;
        _outRead = -_latency;
        _frameCount = 0; _markCount = 0;
        Array.Clear(_recentPeriods); _recentIdx = 0;
        _trackVoiced = false; _lastVoicedPeriod = 0;
        DetectedPitchHz = 0;
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

        for (int i = 0; i < buf.Length; i++)
        {
            _x[(int)(_avail & _mask)] = buf[i];
            _avail++;
        }

        Analyse();
        PlaceMarks();
        Synthesise();

        for (int i = 0; i < buf.Length; i++)
        {
            long o = _outRead++;
            if (o < 0) { buf[i] = 0; continue; }
            int idx = (int)(o & _mask);
            float w = _olaW[idx];
            // normalise overlapping grains; keep a floor so sparse grains (pitch down) are not over-boosted
            buf[i] = w > 1e-3f ? _ola[idx] / Math.Max(w, 0.7f) : 0f;
            _ola[idx] = 0; _olaW[idx] = 0;
        }
    }

    // ───────────────────────── pitch tracking ─────────────────────────

    private void Analyse()
    {
        while (_analysisCenter + _n / 2 <= _avail)
        {
            long start = _analysisCenter - _n / 2;
            double energy = 0;
            for (int i = 0; i < _n; i++)
            {
                double v = _x[(int)((start + i) & _mask)] * _window[i];
                _re[i] = v; _im[i] = 0; energy += v * v;
            }
            for (int i = _n; i < _fftN; i++) { _re[i] = 0; _im[i] = 0; }

            float period = 0;
            double strength = 0;
            if (energy / _n > 1e-8)
            {
                _fft.Forward(_re, _im);
                for (int k = 0; k < _fftN; k++) { _re[k] = _re[k] * _re[k] + _im[k] * _im[k]; _im[k] = 0; }
                _fft.Inverse(_re, _im);
                double r0 = _re[0] + 1e-12;
                int bestP = 0; double bestV = 0;
                for (int p = _minP; p <= _maxP; p++)
                {
                    double v = _re[p] / (r0 * _windowAcf[p]);
                    if (v > bestV) { bestV = v; bestP = p; }
                }
                for (int p = _minP; p < bestP; p++)
                {
                    double v = _re[p] / (r0 * _windowAcf[p]);
                    if (v > bestV * 0.88 && v > 0.5 && _re[p] >= _re[p - 1] && _re[p] >= _re[p + 1]) { bestP = p; bestV = v; break; }
                }
                // continuity: when already voiced, prefer a candidate near the previous period if it is nearly as strong
                if (_trackVoiced && _lastVoicedPeriod > 0 && Math.Abs(bestP - _lastVoicedPeriod) > _lastVoicedPeriod * 0.3)
                {
                    int lo = Math.Max(_minP, (int)(_lastVoicedPeriod * 0.8)), hi = Math.Min(_maxP, (int)(_lastVoicedPeriod * 1.25));
                    int nearP = 0; double nearV = 0;
                    for (int p = lo; p <= hi; p++)
                    {
                        double v = _re[p] / (r0 * _windowAcf[p]);
                        if (v > nearV) { nearV = v; nearP = p; }
                    }
                    if (nearP > 0 && nearV > bestV * 0.8 && nearV > 0.45) { bestP = nearP; bestV = nearV; }
                }
                if (bestP > 0)
                {
                    double a = _re[bestP - 1], b = _re[bestP], c = _re[bestP + 1];
                    double denom = a - 2 * b + c;
                    double frac = Math.Abs(denom) > 1e-12 ? 0.5 * (a - c) / denom : 0;
                    period = (float)(bestP + Math.Clamp(frac, -0.5, 0.5));
                    strength = bestV;
                }
            }

            // voicing decision with hysteresis (avoids flicker at onsets/offsets)
            bool voiced = _trackVoiced ? strength > 0.4 : strength > 0.58;
            if (voiced)
            {
                _recentPeriods[_recentIdx] = period;
                _recentIdx = (_recentIdx + 1) % 3;
                float a = _recentPeriods[0], b = _recentPeriods[1], c = _recentPeriods[2];
                if (a > 0 && b > 0 && c > 0) period = Math.Max(Math.Min(a, b), Math.Min(Math.Max(a, b), c));
                _lastVoicedPeriod = period;
                DetectedPitchHz = _fs / period;
            }
            else
            {
                Array.Clear(_recentPeriods); _recentIdx = 0;
                period = _unvoicedP;
                DetectedPitchHz = 0;
            }
            _trackVoiced = voiced;

            _frames[_frameCount % _frames.Length] = (_analysisCenter, period, voiced);
            _frameCount++;
            _analysisCenter += _hop;
        }
    }

    private (float period, bool voiced) EstimateAt(double pos)
    {
        int count = Math.Min(_frameCount, _frames.Length);
        (float period, bool voiced) best = (_unvoicedP, false);
        long bestDist = long.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var f = _frames[(_frameCount - 1 - i) % _frames.Length];
            long d = Math.Abs(f.center - (long)pos);
            if (d < bestDist) { bestDist = d; best = (f.period, f.voiced); }
            if (f.center < pos - _hop * 2) break;
        }
        return best;
    }

    // ───────────────────────── pitch marks ─────────────────────────

    private void PlaceMarks()
    {
        long lastCenter = _analysisCenter - _hop;
        while (_frameCount > 0 && _nextMark <= lastCenter && _nextMark + _maxP / 4 + _maxP + 1 < _avail)
        {
            var (period, voiced) = EstimateAt(_nextMark);
            long pos = (long)Math.Round(_nextMark);
            var prev = _markCount > 0 ? _marks[(_markCount - 1) % _marks.Length] : default;

            if (voiced)
            {
                // limit period jumps between consecutive voiced marks (pitch does not change 30 % in one period)
                if (prev.voiced && prev.period > 0)
                    period = Math.Clamp(period, prev.period * 0.75f, prev.period * 1.33f);

                int p = Math.Max(4, (int)Math.Round(period));
                int radius = Math.Max(2, p / 4);
                if (prev.voiced && prev.pos > p + 1)
                {
                    // waveform-similarity refinement: choose the offset whose grain best matches the previous grain
                    long best = pos; double bestCorr = double.NegativeInfinity;
                    int half = Math.Min(p, (int)prev.period);
                    for (long cand = pos - radius; cand <= pos + radius; cand += 2)
                    {
                        if (cand - half < 0) continue;
                        double corr = 0, e = 1e-9;
                        for (int j = -half; j < half; j += 2)
                        {
                            float a = _x[(int)((cand + j) & _mask)];
                            float b = _x[(int)((prev.pos + j) & _mask)];
                            corr += a * b; e += a * a;
                        }
                        double norm = corr / Math.Sqrt(e);
                        if (norm > bestCorr) { bestCorr = norm; best = cand; }
                    }
                    // fine step around the coarse best
                    long fineBest = best; double fineCorr = bestCorr;
                    for (long cand = best - 1; cand <= best + 1; cand += 2)
                    {
                        if (cand - half < 0) continue;
                        double corr = 0, e = 1e-9;
                        for (int j = -half; j < half; j++)
                        {
                            float a = _x[(int)((cand + j) & _mask)];
                            float b = _x[(int)((prev.pos + j) & _mask)];
                            corr += a * b; e += a * a;
                        }
                        double norm = corr / Math.Sqrt(e);
                        if (norm > fineCorr) { fineCorr = norm; fineBest = cand; }
                    }
                    pos = fineBest;
                }
                else
                {
                    // first voiced mark after silence/unvoiced: anchor on the energy peak of the local period
                    long best = pos; float bestV = float.NegativeInfinity;
                    for (long cand = pos - radius; cand <= pos + radius; cand++)
                    {
                        if (cand < 2) continue;
                        float v = MathF.Abs(_x[(int)(cand & _mask)]) + MathF.Abs(_x[(int)((cand - 1) & _mask)]) + MathF.Abs(_x[(int)((cand + 1) & _mask)]);
                        if (v > bestV) { bestV = v; best = cand; }
                    }
                    pos = best;
                }
            }
            else
            {
                period = _unvoicedP;
            }
            _marks[_markCount % _marks.Length] = (pos, period, voiced);
            _markCount++;
            _nextMark = pos + period;
        }
    }

    private bool TryNearestMark(double pos, out (long pos, float period, bool voiced) mark)
    {
        mark = default;
        if (_markCount == 0) return false;
        var last = _marks[(_markCount - 1) % _marks.Length];
        if (last.pos < pos) return false;
        int count = Math.Min(_markCount, _marks.Length);
        long bestDist = long.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var m = _marks[(_markCount - 1 - i) % _marks.Length];
            long d = Math.Abs(m.pos - (long)Math.Round(pos));
            if (d < bestDist) { bestDist = d; mark = m; }
            if (m.pos < pos - _maxP) break;
        }
        return true;
    }

    // ───────────────────────── synthesis ─────────────────────────

    private void Synthesise()
    {
        while (TryNearestMark(_nextOut, out var m))
        {
            int p = Math.Max(2, (int)Math.Round(m.period));
            if (m.pos + p >= _avail) return;
            long center = (long)Math.Round(_nextOut);
            if (center - p > _outRead - 1)
            {
                double invP = Math.PI / p;
                for (int j = -p; j <= p; j++)
                {
                    float w = (float)(0.5 + 0.5 * Math.Cos(j * invP));
                    long src = m.pos + j;
                    if (src < 0) continue;
                    int oi = (int)((center + j) & _mask);
                    _ola[oi] += _x[(int)(src & _mask)] * w;
                    _olaW[oi] += w;
                }
            }
            _nextOut += m.voiced ? m.period / _ratio : m.period;
        }
    }
}
