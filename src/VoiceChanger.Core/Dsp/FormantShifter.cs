namespace VoiceChanger.Core.Dsp;

/// <summary>
/// STFT-domain formant shifter with breathiness synthesis.
///
/// For every frame the spectral envelope (vocal-tract resonances) is estimated with an
/// iterative "true envelope" cepstral method whose lifter length adapts to the detected
/// pitch period, so harmonics are excluded from the envelope. The harmonic fine structure is
/// whitened, the envelope is warped along the frequency axis by the formant ratio and
/// re-applied. Phases are left untouched so the pitch is preserved and transients stay crisp.
/// Breathiness adds envelope-shaped aspiration noise above ~1.5 kHz.
/// </summary>
public sealed class FormantShifter
{
    private readonly float _fs;
    private readonly int _n, _hop, _half;
    private readonly Fft _fft;
    private readonly double[] _window;
    private readonly float[] _inWin;     // sliding analysis window (N)
    private readonly double[] _ola;      // overlap-add accumulator (N)
    private readonly FloatFifo _input = new();
    private readonly FloatFifo _output = new();
    private readonly DelayLine _bypassDelay;

    private readonly double[] _re, _im, _cre, _cim;
    private readonly double[] _logMag, _env, _envWarped, _tmp, _corrSmooth;
    private readonly float[] _breathWeight;
    private readonly double[] _lifterTaper;
    private readonly double[] _windowAcf;

    private double _ratio = 1.0;
    private float _breath;
    private bool _bypass = true;
    private uint _rng = 0x9E3779B9;
    private int _defaultLifter;
    private int _minPeriod, _maxPeriod;

    public int LatencySamples => _n;
    /// <summary>Most recent estimated fundamental frequency in Hz (0 when unvoiced). For UI display.</summary>
    public float DetectedPitchHz { get; private set; }

    public FormantShifter(float sampleRate)
    {
        _fs = sampleRate;
        _n = sampleRate > 64000 ? 2048 : 1024;
        _hop = _n / 4;
        _half = _n / 2;
        _fft = new Fft(_n);
        _window = new double[_n];
        for (int i = 0; i < _n; i++) _window[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / _n);
        _inWin = new float[_n];
        _ola = new double[_n];
        _re = new double[_n]; _im = new double[_n];
        _cre = new double[_n]; _cim = new double[_n];
        _logMag = new double[_half + 1];
        _env = new double[_half + 1];
        _envWarped = new double[_half + 1];
        _tmp = new double[_half + 1];
        _corrSmooth = new double[_half + 1];
        _breathWeight = new float[_half + 1];
        for (int k = 0; k <= _half; k++)
        {
            double f = k * sampleRate / _n;
            double t = Math.Clamp((f - 1200) / 2800, 0, 1);
            _breathWeight[k] = (float)(t * t * (3 - 2 * t));
        }
        _lifterTaper = new double[8];
        for (int i = 0; i < 8; i++) _lifterTaper[i] = 0.5 + 0.5 * Math.Cos(Math.PI * i / 8);
        // normalised autocorrelation of the analysis window (compensates the window's lag bias)
        _windowAcf = new double[_n];
        double w0 = 0;
        for (int i = 0; i < _n; i++) w0 += _window[i] * _window[i];
        for (int lag = 0; lag < _n; lag++)
        {
            double s = 0;
            for (int i = 0; i + lag < _n; i++) s += _window[i] * _window[i + lag];
            _windowAcf[lag] = Math.Max(s / w0, 0.05);
        }
        _defaultLifter = (int)(sampleRate / 600);              // ~80 samples @48k
        _minPeriod = (int)(sampleRate / 500);                  // 500 Hz max f0
        _maxPeriod = Math.Min(_half - 1, (int)(sampleRate / 65)); // 65 Hz min f0
        _bypassDelay = new DelayLine(_n + 8);
        Reset();
    }

    /// <summary>Formant ratio to apply (1.0 = unchanged) and breathiness amount 0..1.</summary>
    public void Configure(double formantRatio, float breathiness)
    {
        formantRatio = Math.Clamp(formantRatio, 0.4, 2.5);
        bool bypass = Math.Abs(formantRatio - 1.0) < 0.003 && breathiness < 0.001f;
        if (bypass != _bypass)
        {
            _bypass = bypass;
            if (_bypass) _bypassDelay.Clear(); else ResetStft();
        }
        _ratio = formantRatio;
        _breath = breathiness;
    }

    public void Reset()
    {
        _bypassDelay.Clear();
        ResetStft();
    }

    private void ResetStft()
    {
        _input.Clear();
        _output.Clear();
        _output.WriteZeros(_n);
        Array.Clear(_inWin);
        Array.Clear(_ola);
        Array.Clear(_corrSmooth);
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
            // slide analysis window
            Array.Copy(_inWin, _hop, _inWin, 0, _n - _hop);
            hopBuf.CopyTo(_inWin.AsSpan(_n - _hop));
            ProcessFrame();
            // emit the fully overlapped hop
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

        double frameEnergy = 0;
        for (int k = 0; k <= half; k++)
        {
            double mag = Math.Sqrt(_re[k] * _re[k] + _im[k] * _im[k]);
            frameEnergy += mag * mag;
            _logMag[k] = Math.Log(mag + 1e-9);
        }
        bool silent = frameEnergy < 1e-7;

        // --- pitch estimate via autocorrelation (inverse FFT of the power spectrum) ---
        int lifter = _defaultLifter;
        if (!silent)
        {
            for (int k = 0; k <= half; k++) _tmp[k] = _re[k] * _re[k] + _im[k] * _im[k];
            FillSymmetric(_tmp, _cre, _cim);
            _fft.Inverse(_cre, _cim);
            double r0 = _cre[0] + 1e-12;
            int bestP = 0; double bestV = 0;
            for (int p = _minPeriod; p <= _maxPeriod; p++)
            {
                double v = _cre[p] / (r0 * _windowAcf[p]);
                if (v > bestV) { bestV = v; bestP = p; }
            }
            // prefer the shortest lag that is nearly as strong as the best (avoids octave errors)
            for (int p = _minPeriod; p < bestP; p++)
            {
                double v = _cre[p] / (r0 * _windowAcf[p]);
                if (v > bestV * 0.9 && v > 0.5 && IsLocalMax(p)) { bestP = p; bestV = v; break; }
            }
            if (bestV > 0.45)
            {
                DetectedPitchHz = _fs / bestP;
                lifter = Math.Clamp((int)(bestP * 0.85), 24, half / 2);
            }
            else
            {
                DetectedPitchHz = 0;
            }

            // --- cepstrum of the log magnitude (real, symmetric) ---
            FillSymmetric(_logMag, _cre, _cim);
            _fft.Inverse(_cre, _cim);
            Lifter(lifter, _env);  // _env = smoothed log envelope (first estimate)
            // true-envelope refinement: iterate on max(logMag, env)
            for (int it = 0; it < 2; it++)
            {
                for (int k = 0; k <= half; k++) _tmp[k] = Math.Max(_logMag[k], _env[k]);
                FillSymmetric(_tmp, _cre, _cim);
                _fft.Inverse(_cre, _cim);
                Lifter(lifter, _env);
            }
        }
        else
        {
            DetectedPitchHz = 0;
            Array.Copy(_logMag, _env, half + 1);
        }

        // --- warp envelope along frequency ---
        double invRatio = 1.0 / _ratio;
        double edge = _env[half];
        for (int k = 0; k <= half; k++)
        {
            double src = k * invRatio;
            if (src >= half)
            {
                _envWarped[k] = edge - (src - half) / half * 1.5; // gentle roll-off beyond Nyquist source
            }
            else
            {
                int i0 = (int)src;
                double t = src - i0;
                _envWarped[k] = _env[i0] + (_env[i0 + 1] - _env[i0]) * t;
            }
        }

        // --- resynthesis: whiten, apply warped envelope, add breath noise ---
        float breath = _breath;
        double noiseGain = 0.28 * breath;
        for (int k = 0; k <= half; k++)
        {
            // correction gain in the log domain, smoothed over time so frame-to-frame envelope estimation
            // noise does not modulate the harmonics (the "gurgle" of naive per-frame envelope warping)
            double corr = _envWarped[k] - _env[k];
            corr = _corrSmooth[k] = silent ? corr : 0.6 * _corrSmooth[k] + 0.4 * corr;
            double g = Math.Exp(Math.Clamp(corr, -4.0, 4.1));
            double re = _re[k] * g, im = _im[k] * g;
            if (breath > 0)
            {
                float w = _breathWeight[k];
                if (w > 0)
                {
                    double harmAtt = 1.0 - 0.45 * breath * w;
                    re *= harmAtt; im *= harmAtt;
                    double nm = Math.Exp(_envWarped[k]) * noiseGain * w * NextGaussianMag();
                    double ph = NextUniform() * 2 * Math.PI;
                    re += nm * Math.Cos(ph);
                    im += nm * Math.Sin(ph);
                }
            }
            _re[k] = re; _im[k] = im;
            if (k > 0 && k < half) { _re[n - k] = re; _im[n - k] = -im; }
        }
        _im[0] = 0; _im[half] = 0;

        _fft.Inverse(_re, _im);
        for (int i = 0; i < n; i++) _ola[i] += _re[i] * _window[i];
    }

    /// <summary>Applies a low-quefrency lifter to the cepstrum in _cre/_cim and transforms it back into <paramref name="dest"/> (log envelope).</summary>
    private void Lifter(int cutoff, double[] dest)
    {
        int n = _n;
        for (int i = 0; i < n; i++)
        {
            int q = i <= n / 2 ? i : n - i;
            double w;
            if (q < cutoff) w = 1;
            else if (q < cutoff + 8) w = _lifterTaper[q - cutoff];
            else w = 0;
            _cre[i] *= w;
            _cim[i] = 0;
        }
        _fft.Forward(_cre, _cim);
        for (int k = 0; k <= _half; k++) dest[k] = _cre[k];
    }

    private bool IsLocalMax(int p) => p > 1 && p < _n - 1 && _cre[p] >= _cre[p - 1] && _cre[p] >= _cre[p + 1];

    private void FillSymmetric(double[] halfSpectrum, double[] re, double[] im)
    {
        int n = _n, half = _half;
        for (int k = 0; k <= half; k++) { re[k] = halfSpectrum[k]; im[k] = 0; }
        for (int k = 1; k < half; k++) { re[n - k] = halfSpectrum[k]; im[n - k] = 0; }
    }

    private double NextUniform()
    {
        _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5;
        return _rng * (1.0 / 4294967296.0);
    }

    /// <summary>Rayleigh-distributed magnitude (magnitude of complex Gaussian noise), mean ≈ 1.</summary>
    private double NextGaussianMag()
    {
        double u = 1.0 - NextUniform();
        return Math.Sqrt(-2.0 * Math.Log(u + 1e-12)) * 0.8;
    }
}
