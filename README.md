<p align="center">
  <img src="src/VoiceChanger.App/Assets/app-256.png" width="96" alt="Voice Changer Pro by Cyberkyd" />
</p>

<h1 align="center">Voice Changer Pro by Cyberkyd</h1>

<p align="center">
  <b>Real-time, system-wide voice changer for Windows.</b><br/>
  Ultra-realistic voices, studio-grade clean-up and a background music bed — heard by every app that uses your microphone.
</p>

<p align="center">
  <a href="https://github.com/cyberkyd01/VoiceChangerPro/releases/latest"><img alt="Download" src="https://img.shields.io/badge/Download-Windows%20installer-5865F2?style=for-the-badge&logo=windows" /></a>
  <img alt="Platform" src="https://img.shields.io/badge/Windows-10%20%7C%2011%20x64-0078D6?style=for-the-badge&logo=windows" />
  <img alt=".NET" src="https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet" />
  <img alt="Tests" src="https://img.shields.io/badge/tests-42%20passing-3DD68C?style=for-the-badge" />
</p>

---

## What it does

Voice Changer Pro captures any microphone, transforms your voice in real time and delivers the result
to a virtual microphone that **every application** can use — games, Discord, Zoom, Teams, Google Meet,
softphones, browsers, OBS, Audacity and any recorder. There is nothing to configure in the other apps:
the virtual microphone becomes the Windows default, and a master switch drops you back to your real
voice at any moment without interrupting the call.

**Built for**

| | |
|---|---|
| 📞 **Callers & support agents** | Consistent, clear, professional voice on every call; office noise removed; hold-music or ambience bed on demand. |
| 🎙️ **Podcasters & radio hosts** | Broadcast chain (EQ, de-esser, compressor, limiter) and a looping music bed mixed under the voice before it reaches the recorder. |
| 🎮 **Gamers & streamers** | Realistic character voices in any game or voice chat with low latency and one-click switching. |
| 📖 **Storytellers & narrators** | Believable age and gender voices, vocal tremor and breath for characters, ambience under the narration. |
| 🏫 **Teachers & presenters** | Noise removal, voice isolation and an even, controlled level. |
| 🕶️ **Privacy** | Take calls with a voice that is not recognisably yours. |

---

## Pro features

### Voice engine
- **Pitch-synchronous pitch shifting (TD-PSOLA)** — pitch moves, the vocal tract does not. No chipmunk, no robot: the voice keeps its natural character. Pitch tracked every 5 ms with voicing hysteresis and octave-jump protection.
- **Independent vocal-tract size (formant) control** — STFT spectral-envelope warping with pitch-adaptive true-envelope estimation, smoothed over time.
- **Breathiness synthesis, vocal tremor, rasp/gravel, nasality** — the ingredients of age, mood and character.
- **7-band parametric EQ, de-esser, soft-knee compressor, noise gate, brick-wall limiter** — a full broadcast chain on every microphone.
- **Creative effects** for custom presets: ring-mod, chorus, radio/telephone band-limiter, lo-fi crush, echo, reverb.

### 78 ultra-realistic presets
- **20 female voices** — soft & warm, bright & energetic, deep sultry, girl next door, corporate, southern, late-night host, whispery, presenter, storyteller, playful, yoga instructor, influencer, mom, grandmother, teen, young woman, mature woman, studio singer, nurse.
- **20 male voices** — deep baritone, bass announcer, warm tenor, raspy rocker, soft-spoken, laid-back surfer, executive, drill sergeant, gamer, nerdy, southern gentleman, grandfather, teen, college guy, heavyweight, nervous guy, trailer narrator, late-night DJ, podcast host, sports commentator.
- **Age** (kids to elderly), **Professional** (support agent, news anchor, audiobook narrator, radio DJ, ASMR, e-sports caster …), **Accent flavours** (tonal colorations), **Utilities**.
- Unlimited **custom presets**, favourites, search, duplicate, rename, **JSON import/export** to share with others.

### Noise cancellation & voice isolation (global)
- **Adaptive spectral noise removal** — tracks the noise floor continuously (fans, hum, hiss, keyboard bleed, traffic); no "learn noise" step; up to 30 dB of reduction while the voice keeps its tone.
- **Voice isolation** — a downward expander that pushes other people, TV and room chatter below your close-talking voice, by up to 40 dB.
- Both sit **outside the presets**: switch voices freely, the clean-up stays exactly as you set it. Adjustable live while you talk.

### Background audio bed
- Play **any audio file** (MP3, WAV, M4A/AAC, WMA, FLAC, MP4 audio, AIFF) under your voice on the microphone channel — the other party hears both.
- Loop, play/pause, restart; separate **file volume** and **voice volume** sliders, independent of presets.
- Streamed from disk (hour-long beds are fine), mixed **after** the voice chain so effects and noise removal never alter it.

### System-wide routing & device handling
- Works with **VB-CABLE** (free; the installer sets it up), Virtual Audio Cable, VoiceMeeter, SteelSeries Sonar, NVIDIA Broadcast and Screaming Bee devices — auto-detected, manual override available.
- One click makes the cable the **Windows default microphone** for all roles; restored on exit.
- **Every microphone** gets its own engine and preset; **hot-plug** detection starts new headsets automatically; apply a preset to one mic or all.
- **Live monitoring** on headphones, **5-second test clip** you can loop while tuning, live IN/OUT/gain-reduction/gate/pitch meters.

### Production hardening
- Self-contained single-file build (no .NET install), per-user installer with uninstaller, tray icon with quick toggles, start with Windows, single-instance guard.
- Atomic JSON persistence with schema migrations, rotating logs, crash-safe audio callbacks, automatic recovery when devices vanish and return, inaudible clock-drift compensation.
- **Interference detector**: recognises other voice-changer drivers that silence microphones system-wide (NCH Voxal, MorphVOX, AV Voice Changer) and tells you exactly how to fix it.
- 42 automated tests: pitch-ratio accuracy, formant preservation, level stability, noise-suppression SNR, preset library rules, persistence, background decoding, real-device smoke tests.

---

## Installation

**Installer (recommended)** — download `VoiceChangerPro-Setup-<version>.exe` from the
[Releases page](https://github.com/cyberkyd01/VoiceChangerPro/releases) and run it. No administrator
rights are needed for the app itself (per-user install). The wizard offers to download and install
**VB-CABLE**, the free virtual audio cable that lets other applications hear your changed voice; that
driver step asks for administrator confirmation and may need one reboot. The .NET runtime is bundled.
Uninstall from Settings › Apps.

**Portable** — `VoiceChangerPro.exe` from the release (or from `build.ps1 -Publish`) runs from any
folder; install VB-CABLE separately from <https://vb-audio.com/Cable/>.

## Quick start

1. Start the app — the toolbar pill turns green: *VB-CABLE ready · default mic ✓*.
2. Your microphones are listed on the left and already live. Select one.
3. Put on headphones, switch on **Hear myself**, pick a preset (double-click or *Apply*), fine-tune it on the right.
4. Dial in **Noise removal** and **Voice isolation** while speaking.
5. Optional: **Choose file…** in *Background audio*, press **Play**, balance *File* and *My voice*.
6. In any other app, the microphone is now *CABLE Output (VB-Audio Virtual Cable)*. Use the **Voice effects** switch to go back to your real voice any time.

📘 **Full documentation: [User Guide](docs/USER-GUIDE.md)** — every control, per-app setup (Discord, Zoom, Teams, OBS, games, softphones), recipes and troubleshooting.

---

## Building from source

```powershell
# .NET 8 SDK required (https://dotnet.microsoft.com/download/dotnet/8.0)
.\build.ps1                       # restore, build, unit tests
.\build.ps1 -Publish              # + self-contained single-file build in .\dist
.\build.ps1 -Publish -Installer   # + Windows installer in .\installer\Output (needs Inno Setup 6: winget install JRSoftware.InnoSetup)
.\build.ps1 -Integration          # + real-device tests (opens the default mic and speakers)
```

### Solution layout

```
VoiceChanger.sln
├─ src/VoiceChanger.Core          # engine, DSP, devices, presets, settings, logging (no UI dependency)
│  ├─ Audio/Devices               # WASAPI enumeration + hot-plug, default-device policy, cable + interference detection
│  ├─ Audio/Engine                # VoiceEngine (per mic), OutputSink (render + drift control), BackgroundTrack, EngineManager
│  ├─ Dsp                         # Biquad, FFT, PsolaPitchShifter, FormantShifter, NoiseSuppressor, Dynamics, Effects, VoicePipeline
│  ├─ Presets                     # VoiceProfile (parameter model), VoicePreset, BuiltInPresets (78), PresetStore
│  └─ Settings / Logging
├─ src/VoiceChanger.App           # WPF (Fluent/Mica UI), MVVM, tray, dialogs
├─ installer/                     # Inno Setup script (per-user installer, VB-CABLE bootstrap)
├─ docs/USER-GUIDE.md             # end-user documentation
└─ tests/VoiceChanger.Core.Tests  # DSP correctness, preset rules, persistence, background audio, device smoke tests
```

### Signal chain

```
mic → [global: noise removal → voice isolation] → input gain → high-pass → noise gate
    → pitch shift (TD-PSOLA) → formant shift + breath (STFT) → tremor → rasp → nasality → EQ
    → de-esser → compressor → effects → output gain → limiter → [voice volume] → + background audio
    → virtual cable (+ headphone monitor)
```

**Pitch**: time-domain pitch-synchronous overlap-add. A tracker (zero-padded FFT autocorrelation with
window-bias correction, sub-harmonic guard, continuity preference, median smoothing, voicing hysteresis)
runs every 5 ms; pitch marks are chained one period apart and refined by waveform similarity to the
previous grain; two-period Hann grains are re-spaced at the target period. Grains are never resampled,
so formants are preserved exactly. **Formant**: iterative true-envelope cepstral estimation with a
pitch-following lifter, whitening, frequency-axis warp, temporally smoothed correction gain.
**Noise removal**: STFT Wiener suppressor, minimum-statistics noise tracking, decision-directed a-priori
SNR, peak-preserving gain smoothing. **Voice isolation**: soft-knee downward expander.

### Latency

About 80 ms of DSP latency at 48 kHz (noise stage 11 ms + PSOLA 48 ms + formant 21 ms). With the
default 20 ms buffers the end-to-end figure in the status bar is roughly 150 ms; 10 ms buffers bring
it to about 110 ms on a fast machine.

---

## Data locations

| Item | Path |
|------|------|
| Settings (per-mic configs, favourites, global sliders, background file) | `%AppData%\VoiceChangerPro\settings.json` |
| Custom presets (one JSON per preset) | `%AppData%\VoiceChangerPro\presets\` |
| Logs (14-day rotation) | `%LocalAppData%\VoiceChangerPro\logs\` |

Nothing leaves your PC. The app has no online component apart from the optional VB-CABLE download in the installer.

## Troubleshooting

See the [User Guide › Troubleshooting](docs/USER-GUIDE.md#16-troubleshooting). The most common issue —
a silent microphone caused by another voice changer's driver (NCH Voxal, MorphVOX, AV Voice Changer)
— is detected automatically and explained by the **Mic silent — how to fix** button in the status bar.

## Notes

- Accent presets are tonal flavours only; digital signal processing cannot change pronunciation.
- The virtual cable driver is a separate kernel component (VB-CABLE by VB-Audio, free); the installer downloads it from the vendor and never bundles it.
- Default-microphone switching uses the `IPolicyConfig` COM interface, the same mechanism as popular audio-switching utilities.

## License

Copyright © 2026 Cyberkyd. All rights reserved unless a license file states otherwise.
NAudio (MIT), WPF-UI (MIT) and CommunityToolkit.Mvvm (MIT) are used under their respective licenses.
