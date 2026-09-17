# Voice Changer Pro by Cyberkyd — User Guide

Voice Changer Pro turns any microphone into a different voice in real time and makes that voice
available to **every** application on your PC: games, Discord, Zoom, Teams, Google Meet, WhatsApp
Desktop, softphones, browsers, streaming software and recorders. This guide walks through
installation, first-time setup, everyday use and every control in the application.

---

## Contents

1. [Who it is for](#1-who-it-is-for)
2. [Installation](#2-installation)
3. [How it works (2-minute overview)](#3-how-it-works-2-minute-overview)
4. [First run: five steps to a working setup](#4-first-run-five-steps-to-a-working-setup)
5. [The main window](#5-the-main-window)
6. [Microphones panel](#6-microphones-panel)
7. [Live monitoring and the test clip](#7-live-monitoring-and-the-test-clip)
8. [Voice presets](#8-voice-presets)
9. [Voice editor: every control explained](#9-voice-editor-every-control-explained)
10. [Background noise removal and voice isolation](#10-background-noise-removal-and-voice-isolation)
11. [Background audio (music / ambience under your voice)](#11-background-audio-music--ambience-under-your-voice)
12. [Using it with other apps](#12-using-it-with-other-apps)
13. [Settings](#13-settings)
14. [Tray icon and start with Windows](#14-tray-icon-and-start-with-windows)
15. [Recipes for common situations](#15-recipes-for-common-situations)
16. [Troubleshooting](#16-troubleshooting)
17. [Where your data lives](#17-where-your-data-lives)
18. [Keyboard and mouse tips](#18-keyboard-and-mouse-tips)

---

## 1. Who it is for

| You are a… | What Voice Changer Pro gives you |
|-----------|----------------------------------|
| **Caller / customer-support agent** | A consistent, clear, professional voice on every call; background noise from the office removed; a hold-music or ambience bed you can start and stop. |
| **Podcaster / radio host** | Broadcast-grade chain (EQ, de-esser, compressor, limiter) plus a looping music bed mixed under your voice, all before the audio reaches your recording software. |
| **Gamer / streamer** | A different character voice in any game or Discord with low latency, switchable by one click, with a master switch to drop back to your real voice instantly. |
| **Storyteller / audiobook narrator** | Realistic age and gender voices, vocal tremor and breath for characters, ambience under the narration, all recorded by whatever app you already use. |
| **Teacher / presenter** | Noise removal, voice isolation (other people in the room are pushed down) and a calm, even level. |
| **Privacy-conscious user** | Take calls with a voice that is not recognisably yours. |

---

## 2. Installation

### Requirements
- Windows 10 (1809 or newer) or Windows 11, 64-bit.
- A microphone (built-in, USB, Bluetooth headset, audio interface).
- **VB-CABLE** (free virtual audio cable) so that other apps can receive the changed voice. The installer offers to set it up.

### Using the installer (recommended)
1. Download `VoiceChangerPro-Setup-<version>.exe` from the
   [Releases page](https://github.com/cyberkyd01/VoiceChangerPro/releases).
2. Run it. It installs for the current user by default and needs no administrator rights for the app itself.
3. Keep **Download and install VB-CABLE** ticked (unless you already have a virtual cable). The VB-CABLE
   driver installer opens; click **Install Driver** and confirm the Windows prompt. This is a kernel
   driver, so Windows may ask for one reboot.
4. Tick **Start Voice Changer Pro when setup finishes**.

### Portable use
Copy `VoiceChangerPro.exe` anywhere and run it. Everything (including the .NET runtime) is inside the
file. Install VB-CABLE separately from <https://vb-audio.com/Cable/>.

### Uninstall
Settings › Apps › Installed apps › **Voice Changer Pro by Cyberkyd** › Uninstall. Your presets and
settings stay in `%AppData%\VoiceChangerPro` unless you delete that folder yourself.

---

## 3. How it works (2-minute overview)

```
 Your microphone ──▶ Voice Changer Pro ──▶ "CABLE Input"  (virtual cable, playback side)
                        │                          │
                        │                          ▼
                        │                 "CABLE Output"   (virtual cable, microphone side)
                        │                          │
                        │                          ▼
                        │                 Discord / game / Zoom / recorder … (they think it is a normal mic)
                        │
                        └──▶ your headphones (optional "Hear myself" monitoring)
```

- The app captures your real microphone, processes it, and plays the result into the virtual cable.
- The virtual cable's microphone side, **CABLE Output**, is made the Windows default microphone, so
  applications pick it up automatically. You can also select it manually inside any app.
- The **Voice effects** master switch and the per-microphone **Effect** switch turn processing off
  without breaking the call: your real voice then passes straight through the same route.

---

## 4. First run: five steps to a working setup

1. **Start the app.** The toolbar shows a status pill. Green *VB-CABLE ready · default mic ✓* means
   everything is routed. Red *No virtual cable* means VB-CABLE is not installed: click **Get VB-CABLE
   (free)**, install it, reboot if asked, then click **Refresh**.
2. **Pick a microphone** in the left panel. Connected microphones appear automatically and are usually
   already *Live*. The IN meter at the bottom should move when you speak.
3. **Put on headphones and enable "Hear myself (processed)"** in *Live monitoring* so you hear the result.
   Speakers work too, but your own delayed voice can be distracting and may feed back into the mic.
4. **Choose a preset** in the middle column: double-click a card or press **Apply**. Fine-tune it in the
   *Voice editor* on the right; every slider is live.
5. **Check the other app.** In Discord, Zoom, the game, etc., the microphone should be *CABLE Output
   (VB-Audio Virtual Cable)*. If not, pick it in that app's audio settings or click
   **Set as Windows default mic** in the toolbar.

That is all. From now on the app starts with the last configuration and keeps running in the tray.

---

## 5. The main window

```
┌ Toolbar ───────────────────────────────────────────────────────────────────┐
│ [Voice effects ●]   [● VB-CABLE ready · default mic ✓]        [Refresh][Settings]
├ Left ─────────────┬ Center ────────────────────────┬ Right ─────────────────┤
│ Microphones       │ Voice presets  [search] [cat.] │ Background noise &     │
│  ● Headset  Live  │ ┌────┐ ┌────┐ ┌────┐            │ voice removal (global) │
│  ○ Array   Idle   │ │card│ │card│ │card│            ├────────────────────────┤
│                   │ └────┘ └────┘ └────┘            │ Voice editor           │
│ Live monitoring   │ …                                │  Voice character       │
│ Test clip         ├──────────────────────────────────│  Input & noise gate    │
│                   │ Background audio  ▶ Loop  vol.   │  Equalizer  Dynamics … │
├ Status bar ───────┴──────────────────────────────────┴────────────────────┤
│ IN ▮▮▮ -18 dB   OUT ▮▮▮ -12 dB   GR -3.2 dB   ● gate   pitch 118 Hz   ≈150 ms
└────────────────────────────────────────────────────────────────────────────┘
```

**Toolbar**
- *Voice effects*: master switch for every microphone. Off = pass-through (real voice, still routed).
- *Cable status pill*: routing health. Hover for details. Buttons appear as needed: **Set as Windows
  default mic**, **Get VB-CABLE (free)**.
- *Refresh*: re-scan audio devices (normally automatic).
- *Settings*: preferences, routing, diagnostics.

**Status bar**
- *IN / OUT*: input and output level meters (peak, with peak hold).
- *GR*: gain reduction applied by the compressor and limiter.
- *gate*: lights up while the noise gate is open (voice passing).
- *pitch*: detected fundamental frequency of your voice.
- *≈ latency*: estimated microphone-to-cable delay.
- *Logs*: opens the log folder for support.

---

## 6. Microphones panel

Every capture device that is not itself a virtual cable is listed. New devices appear within a second
of being plugged in (USB, Bluetooth, 3.5 mm headsets on jacks that report insertion).

Per microphone:
- **Active**: capture and route this microphone. New microphones are activated automatically
  (Settings › *Automatically activate newly connected microphones*).
- **Effect**: apply the voice preset (on) or pass the real voice through (off).
- The second line shows the state and the preset name; the third line shows where the audio goes
  (*→ CABLE Input*) or a warning.

Buttons:
- **Activate all**: start every microphone.
- **Copy settings to all**: copy the selected microphone's current voice settings to all others.

Select a microphone to edit it; the editor and meters follow the selection.

---

## 7. Live monitoring and the test clip

**Live monitoring**
- *Hear myself (processed)*: plays the processed voice (and the background audio) to the chosen output.
- *Output device*: your headphones or speakers. Virtual cables are excluded from this list on purpose.
- *Volume*: monitoring level only. It does not change what other apps receive.

**Test clip**
- *Record 5 s*: records five seconds of your **raw** voice.
- *Loop clip*: replaces the live microphone with the recording, so you can move sliders and listen
  without talking. Turn it off to go live again. The clip is never sent to other apps unless Loop is on.
- *Clear*: discard the clip.

---

## 8. Voice presets

The middle column lists 78 built-in presets in six categories plus *My Presets*.

| Category | Contents |
|----------|----------|
| Female Voices | 20 flavours: soft & warm, bright & energetic, husky, girl next door, corporate, southern, late-night host, whispery, presenter, storyteller, playful, yoga instructor, influencer, mom, grandmother, teen, young woman, mature woman, studio singer, nurse |
| Male Voices | 20 flavours: deep baritone, bass announcer, warm tenor, raspy rocker, soft-spoken, surfer, executive, drill sergeant, gamer, nerdy, southern gentleman, grandfather, teen, college guy, heavyweight, nervous guy, trailer narrator, late-night DJ, podcast host, sports commentator |
| Age | kids, teens, young adults, middle-aged, elderly (male and female) |
| Professional | support agent, podcast host, news anchor, audiobook narrator, corporate presenter, e-sports caster, radio DJ, ASMR whisper, conference-call clear, voice-over artist |
| Accent Flavor | tonal colorations (EQ / resonance) inspired by regional broadcast sounds. They do **not** change pronunciation. |
| Utility | clean microphone, broadcast clean, noise gate only, whisper boost, late-night quiet, bypass reference |

Working with presets:
- **Apply**: use the preset on the selected microphone. **All mics**: on every microphone.
- **Double-click** a card = Apply. **Right-click** for the full menu.
- **★ Favorite**: pin to the *★ Favorites* category.
- **Search** by name, description or tag (e.g. `husky`, `radio`, `kid`, `old man`).
- **Save / Save as…** (editor header): store your current settings as a custom preset. Built-ins are
  never overwritten; *Save* on a built-in creates a copy in *My Presets*.
- **Revert**: reload the applied preset. **Neutral**: reset everything to a clean microphone.
- **Duplicate, Rename, Export, Delete** (right-click). Export writes a `.json` file you can share;
  **Import** (top right of the presets panel) loads one or many.
- The header shows the preset name and a **modified** badge when you have changed something since applying it.

All presets are tuned for a typical adult male speaker. If you are a female speaker, presets in the
*Male Voices* category will land lower than described and female presets higher; lower the *Pitch*
slider by three to five semitones to compensate.

---

## 9. Voice editor: every control explained

Every control acts immediately on the live voice. Double-click a value to reset it; the mouse wheel
nudges a slider (hold Shift for bigger steps).

### Voice character
| Control | What it does | Typical realistic range |
|---------|--------------|------------------------|
| **Pitch** (semitones) | Raises or lowers the fundamental frequency only. The pitch-synchronous engine keeps the vocal tract (formants) untouched, so the voice stays natural. | ±5 for adults, up to +8 for a child |
| **Formant (size)** | Vocal-tract size: positive = smaller head/throat (child, female), negative = larger (big man). Independent of pitch. | −2.5 … +4 |
| **Breathiness** | Adds envelope-shaped aspiration noise: airy, intimate or aged voices. | 0–35 % |
| **Rasp / gravel** | Harmonic saturation for rough, gravelly voices. | 0–30 % |
| **Nasality** | Nasal resonance (peak near 1 kHz, dip near 500 Hz). | 0–20 % |
| **Tremor rate / Pitch wobble / Volume wobble** | Vocal tremor of an elderly or nervous speaker. Rate 5–6.5 Hz is natural. | depth ≤ 0.3 |

### Input & noise gate
| Control | What it does |
|---------|--------------|
| **Input gain** | Pre-gain before everything else. |
| **Rumble filter** | High-pass filter removing desk thumps and plosives (80–120 Hz is typical). |
| **Gate** (switch) + **threshold / attack / release / hold** | Mutes the microphone when you are not speaking. Raise the threshold until keyboard clicks and fans disappear but your quietest words still open the gate (watch the *gate* light in the status bar). |

### Equalizer
Low shelf, four fully parametric peak bands (frequency, gain, Q) and a high shelf, plus a low-pass.
Presence (+2 to +3 dB around 3 kHz) makes a voice cut through a call; cutting 2 dB around 250 Hz
removes boominess; a high shelf at 8–10 kHz adds air. **EQ** switch bypasses the whole section.

### Dynamics
- **Compressor** (threshold, ratio, attack, release, knee, makeup): evens out loud and quiet words.
  3:1 with the threshold around −20 dB and 2–4 dB makeup is a good broadcast setting.
- **De-esser** (frequency, threshold, amount): tames harsh "s" sounds; 6.5 kHz suits most voices.

### Effects
Robot (ring modulator), chorus, radio/telephone band-limiter with drive, lo-fi crush, echo and
reverb. None of the built-in realistic presets use these, but they are available for creative use
and for custom presets.

### Output
**Output gain** and the safety **limiter** with its ceiling (−1 dB by default). The limiter guarantees
the signal sent to other apps never clips.

---

## 10. Background noise removal and voice isolation

The panel at the top of the right column is **global**: it applies to every microphone, before the
voice chain, and it is **not part of any preset**. Switch presets as often as you like; these two
sliders keep their values until you change them.

- **Noise removal**: adaptive spectral noise suppression. It continuously tracks the noise floor
  (fans, air conditioning, hum, hiss, distant traffic, keyboard bleed) with no "learn noise" step.
  Slide it up while listening until the noise is gone; the readout shows the estimated noise floor.
  Up to about 30 dB of reduction at 100 %. Because it removes only what it has identified as steady
  noise, your voice keeps its natural tone.
- **Voice isolation**: pushes down everything quieter than your own close-talking voice: other
  people in the room, a TV, café chatter. At higher values only loud, close speech passes; the readout
  shows how much is currently being attenuated.
- **Reset** returns both to zero.

Both react instantly while you speak, so you can tune them during a call.

---

## 11. Background audio (music / ambience under your voice)

The strip under the preset list plays an audio file **on the microphone channel**, under your voice.
The other party (or your recorder) hears your processed voice and the file mixed together; you hear
both in monitoring.

- **Choose file…**: any format Windows can decode: MP3, WAV, M4A/AAC, WMA, FLAC, MP4 audio, AIFF.
  Files are streamed from disk, so hour-long beds are fine.
- **Play**: start/stop. Pausing keeps the position.
- **Loop**: start again when the file ends (on by default). With Loop off, the strip shows *Finished*.
- **↺**: restart from the beginning. **🗑**: remove the file.
- **File** slider: level of the music. **My voice** slider: level of your processed voice, independent
  of presets, so you can balance the two without editing a preset.
- The file is mixed **after** the voice chain: presets, EQ, noise removal and voice isolation never
  alter it.
- If more than one microphone is active, the bed plays through one of them (the status line says
  which) so it is not doubled on the cable.
- File, loop, play state and both volumes are remembered across restarts.

Use cases: podcast intro/bed, radio-style jingles, hold music on calls, ambience for storytelling,
background music for a stream.

---

## 12. Using it with other apps

| App | Where to select the microphone |
|-----|-------------------------------|
| Discord | User Settings › Voice & Video › Input Device → *CABLE Output (VB-Audio Virtual Cable)*. Turn Discord's own noise suppression off to avoid double processing. |
| Zoom | Settings › Audio › Microphone → CABLE Output. Untick "Automatically adjust microphone volume". |
| Microsoft Teams | Settings › Devices › Microphone → CABLE Output. |
| Google Meet / browsers | The browser's site permissions or Meet's settings → CABLE Output. Chrome uses the Windows default mic by default, which the app already sets. |
| Games (Steam, Xbox app, in-game voice) | Most use the Windows default communications device, which the app sets automatically. Otherwise choose CABLE Output in the game's audio options. |
| OBS / Streamlabs | Add an *Audio Input Capture* source → CABLE Output. |
| Audacity / recorders | Recording device → CABLE Output. |
| Softphones (Zoiper, MicroSIP, X-Lite, Dialpad, RingCentral) | Audio settings → microphone → CABLE Output. |
| WhatsApp / Telegram desktop | Settings › Calls / Voice → CABLE Output. |

If an app lets Windows choose ("Default"), nothing needs to be done: the app makes CABLE Output the
default for all roles. To hand the real microphone back to Windows, use Settings › *Restore physical
mic as default* or simply exit the app (it restores the previous default on exit by default).

---

## 13. Settings

**General**
- Start with Windows (minimized to tray), Start minimized, Close minimizes to tray, Show notifications.

**Audio engine**
- *Buffer size*: 10–50 ms. Smaller = lower latency, more CPU, more risk of drop-outs. 20 ms is the
  default; try 10 ms on a fast, quiet machine. Changing it restarts the engines.
- *Automatically activate newly connected microphones* and the preset applied to them.

**Routing (virtual cable)**
- The cable's playback endpoint (the app sends audio here) and microphone endpoint (apps record from
  here). Detected automatically for VB-CABLE, Virtual Audio Cable, VoiceMeeter, SteelSeries Sonar,
  NVIDIA Broadcast and Screaming Bee devices; can be chosen manually.
- *Set cable as Windows default mic*, *Restore physical mic as default*, *Windows sound settings*.
- *Automatically make the cable the default microphone while running* and *Restore the previous
  default microphone on exit*.

**Diagnostics**
- Version, log folder and presets folder with *Open* buttons.

---

## 14. Tray icon and start with Windows

Closing the window keeps the app running in the notification area (configurable). The tray menu has
**Open**, **Voice effects enabled** (master switch) and **Exit**. Double-click the icon to open the
window. With *Start with Windows* on, the app launches minimized at sign-in and your last
configuration is live before you open any call.

---

## 15. Recipes for common situations

**Sound professional on support calls**
Preset *Customer Support Agent* → Noise removal 50–70 % → Voice isolation 30 % if colleagues sit
nearby → Effect on. Optional: a soft hold-music file with the *File* slider at 20 %.

**Podcast with a music bed**
Preset *Podcast Host* (or *Late-Night DJ*) → Background audio: choose your bed, Loop on, *File* at
25–35 %, *My voice* at 100 % → record from CABLE Output in your DAW or recorder.

**Play a character in a game**
Pick a *Male Voices*, *Female Voices* or *Age* preset → Buffer size 10 ms in Settings → monitor with
headphones for a minute to get used to the delay → keep the master switch handy to drop back to your
real voice between rounds.

**Narrate a story with several characters**
Save one custom preset per character (*Save as…*), star them as favourites, switch between them with
one click while recording. Use *Breathiness* and *Tremor* for elderly characters, *Formant* for size.

**Take a call anonymously**
Choose a preset far from your own register (for a male speaker: a *Female Voices* preset; for a female
speaker: a *Male Voices* preset with Pitch lowered a further −3), add Nasality 10 % and a small EQ
change so timbre and register both differ. Keep Effect on for the whole call.

---

## 16. Troubleshooting

**The IN meter never moves / "Mic silent — how to fix" appears**
Another voice changer (NCH Voxal, MorphVOX, AV Voice Changer) is intercepting the microphone at driver
level and passing silence. The button in the status bar names the culprit and the fix (usually:
uninstall that product, restart Windows, press Refresh). If nothing is reported: check Settings ›
Privacy & security › Microphone, the headset's mute switch, and the input level in Windows Sound settings.

**Other apps still hear my real voice**
The app they use is not CABLE Output. Select it in the app (section 12) or click *Set as Windows
default mic*. Some apps must be restarted after the default device changes.

**Status pill says "No virtual cable"**
Install VB-CABLE (link in the toolbar), reboot if asked, press Refresh. If you use another cable
product, choose its endpoints in Settings › Routing.

**Drop-outs or crackling**
Increase the buffer size (Settings › Audio engine), close CPU-heavy apps, avoid Bluetooth microphones
for the lowest latency (their own delay adds up), and keep the laptop on mains power.

**My own voice sounds delayed in my ears**
That is the monitoring path (about 150 ms). Lower the buffer size, or turn *Hear myself* off once the
sound is dialled in; the other party is not affected by your monitoring.

**The voice sounds robotic or "double"**
Pitch or formant is pushed too far for your voice. Stay within ±5 semitones of pitch and ±2.5 of
formant for the most natural result; use EQ, breath and rasp for character instead.

**Feedback / howling**
Monitoring is going to speakers that the microphone can hear. Use headphones or lower the monitor volume.

**Logs for support**
Status bar › *Logs* opens `%LocalAppData%\VoiceChangerPro\logs`. Attach the latest file when reporting an issue.

---

## 17. Where your data lives

| Item | Path |
|------|------|
| Settings (per-mic configs, favourites, global sliders, background file) | `%AppData%\VoiceChangerPro\settings.json` |
| Custom presets (one JSON file each) | `%AppData%\VoiceChangerPro\presets\` |
| Logs (14-day rotation) | `%LocalAppData%\VoiceChangerPro\logs\` |

Nothing is sent anywhere. The app has no online component apart from the optional VB-CABLE download
in the installer.

---

## 18. Keyboard and mouse tips

- Double-click a slider's value: reset to default. Mouse wheel over a slider: fine steps; Shift = coarse.
- Double-click a preset card: apply. Right-click: full preset menu.
- Enter in the *Save preset* dialog saves; Esc cancels.
- The search box clears with the × button; the category box filters instantly.
