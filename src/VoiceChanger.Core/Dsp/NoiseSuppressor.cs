namespace VoiceChanger.Core.Dsp;

/// <summary>
/// Adaptive spectral noise suppressor (STFT Wiener filter). The noise spectrum is tracked
/// continuously with minimum statistics (no "learn noise" step needed), the a-priori SNR uses the
/// decision-directed estimator to avoid musical noise, and the gain floor / aggressiveness follow a
/// single 0..1 amount so the user can dial it in while listening. Sits before the preset chain and is
/// independent of presets. Amount 0 = bypass (constant latency preserved).
/// </summary>
public sealed class NoiseSuppressor
{
    private readonly float _fs;
    private readonly int _n, _hop, _half;
    private readonly Fft _fft;
    private readonly double[] _window, _re, _im;
    private readonly float[] _inWin;
    private readonly double[] _ola;
    private readonly FloatFifo _input = new(), _output = new();
    private readonly DelayLine _bypassDelay;

    private readonly double[] _smoothPsd, _minPsd, _prevSmooth, _noise, _prevGain, _prevPsd, _gain;
    private int _framesSeen;

    private float _amount;
    private bool _bypass = true;
    private double _gainFloor = 0.5, _overSub = 1.0;

    private const double SmoothAlpha = 0.7;   // PSD smoothing
    private const double MinGamma = 0.9995;   // minimum-statistics rise rate (~5 s at 375 frames/s): sustained vowels stay speech
    private const double MinBeta = 0.96;
    private const double NoiseBias = 1.5;
    private const double DdAlpha = 0.96;      // decision-directed weight

    public int LatencySamples => _n;
    /// <summary>Estimated broadband noise floor in dBFS (for UI).</summary>
    public float NoiseFloorDb { get; private set; } = -100;

    public NoiseSuppressor(float sampleRate)
    {
        _fs = sampleRate;
        _n = sampleRate > 64000 ? 1024 : 512;
        _hop = _n / 4;
        _half = _n / 2;
        _fft = new Fft(_n);
        _window = new double[_n];
        for (int i = 0; i < _n; i++) _window[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / _n);
        _re = new double[_n]; _im = new double[_n];
        _inWin = new float[_n]; _ola = new double[_n];
        _smoothPsd = new double[_half + 1]; _minPsd = new double[_half + 1]; _prevSmooth = new double[_half + 1];
        _noise = new double[_half + 1]; _prevGain = new double[_half + 1]; _prevPsd = new double[_half + 1]; _gain = new double[_half + 1];
        _bypassDelay = new DelayLine(_n + 8);
        Reset();
    }

    /// <summary>0 = off, 1 = maximum suppression (about -30 dB residual, aggressive noise estimate).</summary>
    public void Configure(float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        bool bypass = amount < 0.005f;
        if (bypass != _bypass)
        {
            _bypass = bypass;
            if (_bypass) _bypassDelay.Clear(); else ResetStft();
        }
        _amount = amount;
        _gainFloor = Math.Pow(10, -(4 + 26 * amount) / 20.0);   // -4 dB .. -30 dB
        _overSub = 1.0 + 0.8 * amount;                          // over-subtraction of the noise estimate
    }

    public void Reset()
    {
        _bypassDelay.Clear();
        ResetStft();
    }

    private void ResetStft()
    {
        _input.Clear(); _output.Clear(); _output.WriteZeros(_n);
        Array.Clear(_inWin); Array.Clear(_ola);
        Array.Clear(_smoothPsd); Array.Clear(_minPsd); Array.Clear(_prevSmooth); Array.Clear(_noise); Array.Clear(_prevPsd);
        for (int k = 0; k <= _half; k++) { _prevGain[k] = 1; _gain[k] = 1; }
        _framesSeen = 0;
    }

    public void Process(Span<float> buf)
    {
        if (_bypass)
        {
            for (int i = 0; i < buf.Length; i++)
            {
                float x = buf[i];
                buf[i] = _bypassDelay.Read(_n);
                _bypassDelay.Write(x);
            }
            return;
        }

        _input.Write(buf);
        Span<float> hopBuf = stackalloc float[_hop];
        while (_input.Count >= _hop)
        {
            _input.Read(hopBuf);
            Array.Copy(_inWin, _hop, _inWin, 0, _n - _hop);
            hopBuf.CopyTo(_inWin.AsSpan(_n - _hop));
            ProcessFrame();
            for (int i = 0; i < _hop; i++) hopBuf[i] = (float)(_ola[i] / 1.5);
            _output.Write(hopBuf);
            Array.Copy(_ola, _hop, _ola, 0, _n - _hop);
            Array.Clear(_ola, _n - _hop, _hop);
        }
        int got = _output.Read(buf);
        if (got < buf.Length) buf.Slice(got).Clear();
        if (_output.Count > _n * 3) _output.Discard(_output.Count - _n);
    }

    private void ProcessFrame()
    {
        int n = _n, half = _half;
        for (int i = 0; i < n; i++) { _re[i] = _inWin[i] * _window[i]; _im[i] = 0; }
        _fft.Forward(_re, _im);
        _framesSeen++;

        double noiseSum = 0;
        for (int k = 0; k <= half; k++)
        {
            double p = _re[k] * _re[k] + _im[k] * _im[k];

            // smoothed PSD and minimum-statistics noise tracking (Doblinger)
            double s = SmoothAlpha * _smoothPsd[k] + (1 - SmoothAlpha) * p;
            if (_framesSeen < 4) { _minPsd[k] = s; }
            else if (_minPsd[k] < s)
                _minPsd[k] = MinGamma * _minPsd[k] + (1 - MinGamma) / (1 - MinBeta) * (s - MinBeta * _prevSmooth[k]);
            else
                _minPsd[k] = s;
            _prevSmooth[k] = _smoothPsd[k];
            _smoothPsd[k] = s;

            double noise = Math.Max(_minPsd[k] * NoiseBias, 1e-12);
            // during clear speech absence (low posterior SNR) track the noise faster
            if (p < noise * 2.5) noise = 0.9 * _noise[k] + 0.1 * Math.Max(p, 1e-12);
            else noise = Math.Max(_noise[k], noise * 0.999);
            _noise[k] = noise;
            noiseSum += noise;

            double noiseEff = noise * _overSub;
            double gammaPost = p / noiseEff;
            double xi = DdAlpha * (_prevGain[k] * _prevGain[k] * _prevPsd[k] / noiseEff) + (1 - DdAlpha) * Math.Max(gammaPost - 1, 0);
            double g = xi / (1 + xi);
            _gain[k] = Math.Max(g, _gainFloor);
            _prevPsd[k] = p;
        }

        // peak-preserving smoothing across frequency: isolated low-gain bins next to a kept bin are lifted
        // (reduces musical noise) while harmonic peaks are never pulled down by their neighbours.
        double gPrevBin = _gain[0];
        for (int k = 1; k < half; k++)
        {
            double cur = _gain[k];
            double neighbour = Math.Max(gPrevBin, _gain[k + 1]);
            _gain[k] = Math.Max(cur, 0.6 * neighbour);
            gPrevBin = cur;
        }

        for (int k = 0; k <= half; k++)
        {
            double g = _gain[k];
            _prevGain[k] = g;
            _re[k] *= g; _im[k] *= g;
            if (k > 0 && k < half) { _re[n - k] = _re[k]; _im[n - k] = -_im[k]; }
        }
        _im[0] = 0; _im[half] = 0;

        // noise floor readout: mean noise PSD → dBFS (window-normalised)
        double meanNoise = noiseSum / (half + 1) / (n * 0.375);
        NoiseFloorDb = (float)Math.Max(-100, 10 * Math.Log10(meanNoise + 1e-12));

        _fft.Inverse(_re, _im);
        for (int i = 0; i < n; i++) _ola[i] += _re[i] * _window[i];
    }
}

/// <summary>
/// Downward expander used as "voice isolation": sounds quieter than the close-talking voice
/// (background talkers, TV, room chatter) are pushed further down. Amount 0..1 sets threshold and ratio.
/// </summary>
public sealed class VoiceIsolator
{
    private readonly float _fs;
    private float _thresholdDb = -60, _ratio = 1, _maxReductionDb = 0;
    private float _env, _gainDb;
    private readonly float _detAttack, _detRelease, _gainAttack, _gainRelease;

    public bool Active { get; private set; }
    public float GainReductionDb { get; private set; }

    public VoiceIsolator(float sampleRate)
    {
        _fs = sampleRate;
        _detAttack = DspUtil.TimeConstant(2f, sampleRate);
        _detRelease = DspUtil.TimeConstant(60f, sampleRate);
        _gainAttack = DspUtil.TimeConstant(4f, sampleRate);     // reduction applied quickly
        _gainRelease = DspUtil.TimeConstant(120f, sampleRate);  // recovery (opening) slower
    }

    /// <summary>0 = off; 1 = threshold -22 dBFS, 4:1 expansion, up to 40 dB reduction.</summary>
    public void Configure(float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        Active = amount > 0.005f;
        _thresholdDb = -62f + 40f * amount;
        _ratio = 1f + 3f * amount;
        _maxReductionDb = 12f + 28f * amount;
    }

    public void Reset() { _env = 0; _gainDb = 0; GainReductionDb = 0; }

    public void Process(Span<float> buf)
    {
        if (!Active) { GainReductionDb = 0; return; }
        float minGain = 0;
        for (int i = 0; i < buf.Length; i++)
        {
            float x = buf[i];
            float a = MathF.Abs(x);
            _env += (a > _env ? _detAttack : _detRelease) * (a - _env);
            float levelDb = DspUtil.LinearToDb(_env);
            float under = _thresholdDb - levelDb; // positive when below threshold
            float target = 0f;
            if (under > 0)
            {
                // soft knee of 6 dB
                float knee = 6f;
                float u = under < knee ? under * under / (2f * knee) : under - knee / 2f;
                target = -Math.Min(u * (_ratio - 1f), _maxReductionDb);
            }
            _gainDb += (target < _gainDb ? _gainAttack : _gainRelease) * (target - _gainDb);
            if (_gainDb < minGain) minGain = _gainDb;
            buf[i] = x * DspUtil.DbToLinear(_gainDb);
        }
        GainReductionDb = minGain;
    }
}
