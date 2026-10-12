# Voice Changer Pro for Mac: installation guide

Voice Changer Pro changes your voice live, and every app on your Mac hears the changed voice:
Zoom, Google Meet, Microsoft Teams, Discord, FaceTime, WhatsApp, OBS and games.

## Does it work on my Mac?

| You need | How to check |
|---|---|
| A Mac with an **Apple chip** (M1, M2, M3, M4 or newer). Every MacBook Air, MacBook Pro, Mac mini, iMac and Mac Studio sold since late 2020 has one | Apple menu  › **About This Mac**. The *Chip* line must start with **Apple M** |
| **macOS 12 Monterey** or newer | Same window, the line under the Mac's name. Tested on macOS 15 Sequoia |
| Headphones (recommended) | To hear yourself without feedback |

Macs with an Intel processor are not supported by this download.

## Step 1: Install BlackHole (the virtual microphone)

Apps on a Mac can only record from a microphone. **BlackHole** is a free virtual microphone.
Voice Changer Pro sends your changed voice into it, and your apps record from it.

Choose one way:

- **Website:** go to <https://existential.audio/blackhole/>, enter your email to get the free
  download link, and download **BlackHole 2ch**. Open the downloaded `.pkg` file and follow the
  installer. It asks for your Mac password.
- **Homebrew** (if you already use it): open Terminal and run

  ```bash
  brew install blackhole-2ch
  ```

You can install BlackHole after Voice Changer Pro too. The app shows a **Get BlackHole 2ch** button
until it finds it. If BlackHole doesn't appear after installing, restart the Mac.

## Step 2: Download and install Voice Changer Pro

1. Download **[VoiceChangerPro-Mac-AppleSilicon.dmg](https://github.com/cyberkyd01/VoiceChangerPro/releases/latest/download/VoiceChangerPro-Mac-AppleSilicon.dmg)**
   (direct link to the newest version). It is also listed under **Assets** on the
   [latest release](https://github.com/cyberkyd01/VoiceChangerPro/releases/latest) page.
2. Double-click the downloaded file. Drag **Voice Changer Pro** onto the **Applications** folder.
3. Eject the *Voice Changer Pro* disk in Finder.

**Optional: check the file is intact.** In Terminal run the command below and compare the result
with the SHA-256 value printed on the release page.

```bash
shasum -a 256 ~/Downloads/VoiceChangerPro-Mac-AppleSilicon.dmg
```

## Step 3: Open it the first time

The app is not notarized by Apple (that needs a paid Apple developer account), so macOS blocks it
the first time. You only do this once:

1. Open **Applications** and double-click **Voice Changer Pro**.
2. macOS says it *could not verify "Voice Changer Pro" is free of malware*. Click **Done**.
3. Open  › **System Settings** › **Privacy & Security** and scroll down to **Security**.
4. Next to *"Voice Changer Pro" was blocked*, click **Open Anyway**, then confirm with your password
   or Touch ID.
5. Click **Open Anyway** once more in the dialog.

**Faster alternative:** run this in Terminal, then open the app normally.

```bash
xattr -dr com.apple.quarantine "/Applications/Voice Changer Pro.app"
```

If macOS ever says the app *is damaged and can't be opened*, run the same command. It means the
download quarantine flag is still set, not that the file is broken.

## Step 4: Allow the microphone

The first time the app starts, macOS asks: *"Voice Changer Pro" would like to access the microphone*.
Click **Allow**.

If you clicked *Don't Allow*, the app shows a red message. Open  › **System Settings** ›
**Privacy & Security** › **Microphone**, switch **Voice Changer Pro** on, then click **Refresh** in
the app.

## Step 5: Choose a voice

1. **Your microphone:** pick the microphone you speak into, for example *MacBook Pro Microphone*
   or a USB headset.
2. **Virtual microphone:** it should show **BlackHole 2ch**. Leave
   *Make it the Mac's microphone while running* switched on. Every app that uses the default
   microphone then hears your changed voice.
3. **Voices:** click a voice in the list. Filter by category or search, for example *deep*,
   *female* or *radio*.
4. **Current voice:** fine-tune pitch, formant (voice size), breathiness, rasp and reverb.
   Double-click a slider to reset it. *Reset to preset* undoes your changes.
5. **Hear myself:** put on headphones, choose them in the list and switch it on to hear the result.
6. The **Voice effects** switch at the top turns the effect off instantly, so others hear your
   real voice through the same microphone. **Stop** stops everything and gives your real
   microphone back to all apps.

The app remembers your choices. If the voice changer was running when you quit, it starts
automatically the next time you open the app.

## Step 6: Use it in your apps

Most apps use the Mac's default microphone, so they work straight away. If an app has its own
microphone setting, choose **BlackHole 2ch** (or *Same as System*):

| App | Where |
|---|---|
| Zoom | Settings › Audio › Microphone |
| Google Meet / browser calls | The call's settings (⋮) › Audio › Microphone |
| Microsoft Teams | Settings › Devices › Microphone |
| Discord | User Settings › Voice & Video › Input Device |
| FaceTime | Video menu › Microphone |
| OBS Studio | Sources › Audio Input Capture › BlackHole 2ch |

**Important:** never choose BlackHole as your **speaker** or **output** (in System Settings › Sound
› Output, or in a call app). Other people's voices would be sent back to them as an echo, and you
would hear nothing. Keep the output on your headphones or speakers.

When you quit Voice Changer Pro, your real microphone becomes the Mac's microphone again.
If you picked *BlackHole 2ch* by hand inside an app, switch that app back to your real microphone
when you stop using the voice changer, because BlackHole is silent while the app is closed.

## Troubleshooting

| Problem | Fix |
|---|---|
| *Apple could not verify…* or *…is damaged and can't be opened* | Follow Step 3, or run the `xattr` command shown there |
| Red message: *No sound is coming from the microphone* | Allow the microphone (Step 4). Check that the microphone isn't muted and that the right one is selected |
| Others hear **nothing** | Voice Changer Pro must be running (status shows *Running*). The call app must use *BlackHole 2ch* or the default microphone. Check that you aren't muted in the call |
| Others hear your **real** voice | Switch on **Voice effects** at the top. In the call app, choose *BlackHole 2ch* as the microphone |
| Others hear an **echo** of themselves | Your Mac's or the call app's **output** is set to BlackHole. Set it to your headphones or speakers |
| **Feedback** or howling | *Hear myself* plays through speakers that the microphone picks up. Use headphones or switch *Hear myself* off |
| Your voice sounds **delayed in your ears** | About 100 ms is normal for live voice changing. Bluetooth headphones add more delay. Others don't hear your monitoring, so you can switch *Hear myself* off |
| **AirPods** sound muffled | When the AirPods microphone is in use, macOS switches them to low-quality call mode. Select *MacBook Pro Microphone* (or another mic) in the app and keep the AirPods for listening |
| A microphone you plugged in doesn't appear | Click **Refresh**. The app also notices new devices on its own after a few seconds |
| **Get BlackHole 2ch** button doesn't go away after installing | Click **Refresh**. If BlackHole still doesn't appear, restart the Mac |

**Logs for support:** click **Logs** in the app, or open `~/Library/Logs/VoiceChangerPro` in Finder
(Go menu › Go to Folder).

## Uninstall

1. Quit Voice Changer Pro and drag it from **Applications** to the Trash.
2. Optional: in Finder use Go › Go to Folder and delete
   `~/Library/Application Support/VoiceChangerPro` and `~/Library/Logs/VoiceChangerPro`.
3. Optional: remove BlackHole with `brew uninstall blackhole-2ch`, or with the uninstaller from the
   BlackHole website.

## Build from source

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and the Xcode command line
tools (`xcode-select --install`).

```bash
./build-mac.sh
```

The app bundle and the disk image are written to `dist-mac/`. The build runs a self-test that checks
the voice processing. `VoiceChangerPro --selftest --live` additionally runs your real microphone through
the voice chain for three seconds with the output muted.
