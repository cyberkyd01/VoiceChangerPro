# Install Voice Changer Pro by Cyberkyd on Windows

This guide takes you from a clean Windows PC to a working voice changer that Discord, Zoom, Teams,
games and any other app can use as a microphone. You can install the prebuilt release (installer
or portable `.exe`) or build it from source.

All commands are for **Windows PowerShell** (Start menu → type `PowerShell` → *Windows PowerShell*).
Run them in a normal window unless a step says it needs administrator rights.

## At a glance

| | |
|---|---|
| Supported Windows | Windows 10 version 1809 (build 17763) or newer, or Windows 11, 64-bit (x64) |
| Latest release | [`v1.0.0`](https://github.com/cyberkyd01/VoiceChangerPro/releases/tag/v1.0.0): `VoiceChangerPro-Setup-1.0.0.exe` (installer, 51 MB) and `VoiceChangerPro.exe` (portable, 170 MB) |
| .NET | Bundled. Users install nothing extra. Building from source needs the **.NET 8 SDK** (the project targets `net8.0-windows`) |
| Virtual audio cable | **VB-CABLE** by VB-Audio (free donationware). It is a kernel audio driver, so installing it needs **administrator rights** and a **reboot** |
| Install folder (installer) | Default, for you only: `%LocalAppData%\Programs\Voice Changer Pro by Cyberkyd` (no admin). Optional, for all users: `C:\Program Files\Voice Changer Pro by Cyberkyd` (needs admin) |
| Your data | Settings: `%AppData%\VoiceChangerPro\settings.json`. Custom presets: `%AppData%\VoiceChangerPro\presets\`. Logs: `%LocalAppData%\VoiceChangerPro\logs\` |
| What it changes on your system | The VB-CABLE driver, only if you install it. While the app runs, the Windows default microphone is set to *CABLE Output*, and the previous one is restored when you exit. If you turn on *Start with Windows*, it adds one entry under `HKCU\...\Run`. |
| What it never does | It installs no services and no other drivers, changes no firewall settings, and sends nothing online. The only download is the optional VB-CABLE package from the vendor during setup |
| Code signing | The release binaries are **not** code-signed, so Windows SmartScreen may warn you the first time you run them (see [step 2](#2-install-the-app)) |

## Requirements

- A 64-bit Windows 10 (1809+) or Windows 11 PC. ARM64 Windows is not tested. To check your version:

  ```powershell
  Get-ComputerInfo -Property OsName, OsBuildNumber, OsArchitecture
  ```

  `OsBuildNumber` must be **17763 or higher** and `OsArchitecture` must be **64-bit**.
- A microphone: built-in, USB, Bluetooth headset or audio interface. **Headphones** are strongly recommended for monitoring your own voice.
- About 250 MB of free disk space and an internet connection for the downloads.
- Administrator rights **once**, to install the VB-CABLE driver. The app itself does not need them.
- For building from source only: Git, the .NET 8 SDK and, if you want the installer, Inno Setup 6 ([step 3](#3-build-from-source-optional)).

## 1. Install the virtual audio cable (VB-CABLE)

**Why you need it:** Windows has no built-in way for one program to act as a microphone for other
programs. VB-CABLE adds a pair of virtual devices:

```
your mic → Voice Changer Pro → "CABLE Input"  (playback device: the app writes your changed voice here)
                                     ↓
                               "CABLE Output" (recording device: Discord, Zoom, games… use this as the mic)
```

Without it the app still runs and you can hear yourself, but no other app can hear your changed voice.

**Already have a virtual cable?** The app also detects Virtual Audio Cable, VoiceMeeter, SteelSeries
Sonar, NVIDIA Broadcast and Screaming Bee devices. You can skip this step and choose that cable later
in *Settings › Routing*.

You can install VB-CABLE in either of two ways:

- **With the app installer (easiest).** The Voice Changer Pro installer has a ticked task called *Download and install VB-CABLE*. It only shows that task if VB-CABLE isn't installed yet. If you plan to use the installer, skip ahead to [step 2](#2-install-the-app) and leave the task ticked.
- **Manually.** Use this for the portable `.exe`, a source build, or when the installer reports that the download failed. These commands download the same vendor package the installer uses, unpack it to `Downloads\VBCABLE`, and open the driver setup with a UAC (administrator) prompt:

  ```powershell
  $zip = "$env:USERPROFILE\Downloads\VBCABLE_Driver_Pack45.zip"
  $dir = "$env:USERPROFILE\Downloads\VBCABLE"
  curl.exe -L -o $zip https://download.vb-audio.com/Download_CABLE/VBCABLE_Driver_Pack45.zip
  Expand-Archive -LiteralPath $zip -DestinationPath $dir -Force
  Start-Process -FilePath "$dir\VBCABLE_Setup_x64.exe" -Verb RunAs
  ```

  If that link stops working, download the current pack from <https://vb-audio.com/Cable/> and run `VBCABLE_Setup_x64.exe` from it as administrator.

  Approve the UAC prompt, then click **Install Driver**. The window may show *Not responding* for a
  few seconds. Wait for the confirmation dialog. **Keep the `Downloads\VBCABLE` folder**, because the
  same program removes the driver later.

**Restart Windows.** VB-Audio requires a reboot to finish installing the driver. Save your work first:

```powershell
Restart-Computer
```

**Check it worked** (after the reboot):

```powershell
Get-PnpDevice -Class AudioEndpoint -PresentOnly | Where-Object FriendlyName -like '*VB-Audio*' | Select-Object Status, FriendlyName
```

You should see at least `CABLE Input (VB-Audio Virtual Cable)` and `CABLE Output (VB-Audio Virtual Cable)` with status `OK`.

## 2. Install the app

Choose **one** of these options.

### Option A: Installer (recommended)

1. Download the installer and check that the file is intact. You can also download it from the
   [Releases page](https://github.com/cyberkyd01/VoiceChangerPro/releases/latest) in your browser.

   ```powershell
   $setup = "$env:USERPROFILE\Downloads\VoiceChangerPro-Setup-1.0.0.exe"
   curl.exe -L -o $setup https://github.com/cyberkyd01/VoiceChangerPro/releases/download/v1.0.0/VoiceChangerPro-Setup-1.0.0.exe
   (Get-FileHash $setup -Algorithm SHA256).Hash -eq '02E40A9C68897DBE5714BCA73BBB47E3A1CA0EBC175FEFA6F5ADCA42A7D69432'
   ```

   The last line must print `True`. If it prints `False`, delete the file and download it again.

2. Start the installer:

   ```powershell
   Start-Process "$env:USERPROFILE\Downloads\VoiceChangerPro-Setup-1.0.0.exe"
   ```

   - **SmartScreen:** if Windows shows *Windows protected your PC*, click **More info** and then **Run anyway**. The installer isn't code-signed, which is why Windows can't name the publisher. You checked the file's hash above. This warning mostly appears for files downloaded in a browser.
   - **Install mode:** choose **Install for me only**. This needs no administrator rights. *Install for all users* puts the app in `C:\Program Files` and asks for admin rights.
   - **Tasks** (all ticked by default):
     - *Create a desktop shortcut*.
     - *Download and install VB-CABLE*. This task only appears if no VB-CABLE is installed. Leave it ticked unless you already have another virtual cable. After the app is copied, a message explains the next part. Then the VB-CABLE setup opens with a UAC prompt. Click **Install Driver**, close that window, and **restart Windows** when it's convenient.
     - *Start Voice Changer Pro when setup finishes*.

3. Check that it's installed (for an *Install for me only* install):

   ```powershell
   Test-Path "$env:LOCALAPPDATA\Programs\Voice Changer Pro by Cyberkyd\VoiceChangerPro.exe"
   ```

   This should print `True`. The Start menu now has **Voice Changer Pro by Cyberkyd**.

### Option B: Portable `.exe` (no installer)

The portable file contains everything, including the .NET runtime. It runs from any folder and
writes nothing outside your user profile. Install VB-CABLE separately ([step 1](#1-install-the-virtual-audio-cable-vb-cable), manual option).

```powershell
$dir = "$env:USERPROFILE\VoiceChangerPro-Portable"
New-Item -ItemType Directory -Force -Path $dir | Out-Null
curl.exe -L -o "$dir\VoiceChangerPro.exe" https://github.com/cyberkyd01/VoiceChangerPro/releases/download/v1.0.0/VoiceChangerPro.exe
(Get-FileHash "$dir\VoiceChangerPro.exe" -Algorithm SHA256).Hash -eq '26311E4E93E04A9BDBDCCD0C6DF1AED058F14EC9399FADA7C62FDBD4E2FE41B6'
```

The last line must print `True`. Start it with:

```powershell
Start-Process "$env:USERPROFILE\VoiceChangerPro-Portable\VoiceChangerPro.exe"
```

Windows may show the same SmartScreen warning as for the installer (**More info** → **Run anyway**).
The first start takes a few seconds longer while .NET unpacks its native libraries to
`%TEMP%\.net\VoiceChangerPro`.

## 3. Build from source (optional)

Skip this section if you installed a release. You need a **Windows 10/11 x64** PC: the app is
a WPF (`net8.0-windows`) project and won't build on macOS or Linux.

### 3.1 Install the build tools

[winget](https://learn.microsoft.com/windows/package-manager/winget/) ships with Windows 10 (1809+) and 11 as *App Installer*.

```powershell
winget install --id Git.Git -e
winget install --id Microsoft.DotNet.SDK.8 -e
```

Both installers are machine-wide, so Windows asks for administrator rights. **Close PowerShell and
open a new window** so that `git` and `dotnet` are on your PATH. Then check:

```powershell
dotnet --list-sdks
```

At least one line must start with `8.0.` (for example `8.0.4xx [C:\Program Files\dotnet\sdk]`).

<details>
<summary>No administrator rights? Install the .NET 8 SDK just for your user instead</summary>

`build.ps1` supports a per-user SDK through the `DOTNET_ROOT` variable. This installs the SDK into
`%LocalAppData%\Microsoft\dotnet` and changes nothing system-wide. You'll still need Git, or use
**Code → Download ZIP** on GitHub instead of `git clone`.

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
curl.exe -L -o "$env:TEMP\dotnet-install.ps1" https://dot.net/v1/dotnet-install.ps1
& "$env:TEMP\dotnet-install.ps1" -Channel 8.0 -InstallDir "$env:LOCALAPPDATA\Microsoft\dotnet"
```

In **every** new PowerShell window where you build, first run:

```powershell
$env:DOTNET_ROOT = "$env:LOCALAPPDATA\Microsoft\dotnet"
```

</details>

To build the Windows installer yourself, you also need Inno Setup 6. You don't need it for a normal build or the portable `.exe`.

```powershell
winget install --id JRSoftware.InnoSetup -e
```

### 3.2 Get the code

```powershell
cd $env:USERPROFILE
git clone https://github.com/cyberkyd01/VoiceChangerPro.git
cd VoiceChangerPro
```

### 3.3 Allow the build script to run (this window only)

By default Windows blocks `.ps1` scripts. This command allows them **only in the current PowerShell
window**. Nothing is changed permanently, and the setting ends when you close the window.

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
```

### 3.4 Build

`build.ps1` always restores NuGet packages, builds the solution, and runs the unit tests. A failing
test stops the build. These are the parameters it accepts:

| Command | What it does | Output |
|---|---|---|
| `.\build.ps1` | Restore, build (Release), run unit tests | `src\VoiceChanger.App\bin\Release\net8.0-windows\` |
| `.\build.ps1 -Publish` | The above, plus a self-contained single-file x64 build that needs no .NET on the target PC | `dist\VoiceChangerPro.exe` (+ `dist\README.md`) |
| `.\build.ps1 -Publish -Installer` | The above, plus the Windows installer (needs Inno Setup 6). `-Installer` needs `dist\VoiceChangerPro.exe`, so pass `-Publish` with it | `installer\Output\VoiceChangerPro-Setup-1.0.0.exe` |
| `.\build.ps1 -Integration` | Also runs the real-device tests (sets `VCP_INTEGRATION=1`, opens your default mic and speakers) | — |
| `-Configuration Debug` | Builds Debug instead of the default `Release`. You can combine it with any of the above. The script's help text says Debug, but the actual default is Release | — |

For the usual case (a portable build you can run right away):

```powershell
.\build.ps1 -Publish
```

You should see `Using .NET SDK 8.0.x`, the test summary, `Published ...\dist\VoiceChangerPro.exe (… MB)`
and finally `Done.`

### 3.5 Run your build

```powershell
.\dist\VoiceChangerPro.exe
```

You can also double-click **`Start Voice Changer Pro.cmd`** in the repository folder. It launches
`dist\VoiceChangerPro.exe`, so run `-Publish` first. If you built the installer, run
`installer\Output\VoiceChangerPro-Setup-1.0.0.exe` and follow [Option A](#option-a-installer-recommended) from the *Start the installer* part.

Install VB-CABLE as in [step 1](#1-install-the-virtual-audio-cable-vb-cable) (manual option) if you haven't yet.

## 4. First run: pick your devices

1. **Start the app** from the Start menu (*Voice Changer Pro by Cyberkyd*), the desktop shortcut, or your portable/built `.exe`. Only one copy runs at a time. Starting it again just brings the window back.
2. **Check the status pill** in the toolbar:
   - Green **VB-CABLE ready · default mic ✓** means routing is working.
   - Red **No virtual cable** means VB-CABLE is missing or Windows hasn't rebooted since you installed it. Do [step 1](#1-install-the-virtual-audio-cable-vb-cable), reboot, and press **Refresh**.
3. **Input (your real microphone):** connected microphones are listed on the left and switched on automatically. Select yours and speak. The **IN** meter in the status bar should move.
4. **Output (the virtual cable):** the app sends the processed voice to *CABLE Input*. It also makes *CABLE Output* the Windows default microphone for both the *Default* and *Communications* roles. To check or change this, open **Settings › Routing (virtual cable)**:
   - Playback endpoint: `CABLE Input (VB-Audio Virtual Cable)`.
   - Microphone endpoint: `CABLE Output (VB-Audio Virtual Cable)`.
   - *Automatically make the cable the default microphone while running* and *Restore the previous default microphone on exit* are on by default.
5. **Monitoring (optional):** put on headphones. In **Live monitoring**, choose them as the *Output device* and switch on **Hear myself (processed)**. Virtual cables are left out of this list on purpose.
6. **Choose a voice:** double-click a preset card, or select one and press **Apply**. Fine-tune it on the right.
7. **Select the microphone in your other apps.** Apps that use the Windows *Default* microphone pick it up automatically. Otherwise, choose **CABLE Output (VB-Audio Virtual Cable)** as the *microphone / input device*:

   | App | Where |
   |---|---|
   | Discord | User Settings › Voice & Video › Input Device. Turn Discord's own noise suppression off to avoid double processing |
   | Zoom | Settings › Audio › Microphone. Untick *Automatically adjust microphone volume* |
   | Microsoft Teams | Settings › Devices › Microphone |
   | Google Meet / browsers | Meet's settings or the site's microphone permission. Chrome follows the Windows default |
   | OBS / Streamlabs | Add an *Audio Input Capture* source → CABLE Output |
   | Games | Usually follow the Windows default communications device. Otherwise use the in-game voice settings |
   | Softphones, WhatsApp/Telegram desktop | Their audio/call settings → microphone |

   **Leave the speaker/output device in those apps set to your headphones or speakers. Never set it to *CABLE Input*.**
   If you do, the other people's voices are fed back into your virtual microphone and they hear an echo.

The full manual, covering every control, per-app setup, recipes and the tray icon, is in the [User Guide](docs/USER-GUIDE.md).

## Updating

Settings and custom presets live in `%AppData%\VoiceChangerPro` and are kept across updates. Before
any update, **exit the app** with the tray icon's **Exit** command, so it gives your real microphone
back to Windows cleanly.

**Check the versions:**

```powershell
(Invoke-RestMethod https://api.github.com/repos/cyberkyd01/VoiceChangerPro/releases/latest).tag_name
(Get-Item "$env:LOCALAPPDATA\Programs\Voice Changer Pro by Cyberkyd\VoiceChangerPro.exe").VersionInfo.FileVersion
```

The first line shows the latest release, for example `v1.0.0`. The second shows your installed version, for example `1.0.0.0`, for an *Install for me only* install.

- **Installer:** download the new `VoiceChangerPro-Setup-<version>.exe` from the [Releases page](https://github.com/cyberkyd01/VoiceChangerPro/releases/latest) and run it. It upgrades the existing installation in place, in the same folder, with the same Start menu entry. Compare its SHA-256 with the `sha256:` digest shown next to the file on the release page:

  ```powershell
  Get-FileHash "$env:USERPROFILE\Downloads\VoiceChangerPro-Setup-<version>.exe" -Algorithm SHA256
  ```

- **Portable:** download the new `VoiceChangerPro.exe` over the old one in `%USERPROFILE%\VoiceChangerPro-Portable`. Use the command from Option B with the new tag in the URL. If *Start with Windows* is on, its entry updates itself the next time the app starts.
- **Source build:**

  ```powershell
  cd $env:USERPROFILE\VoiceChangerPro
  git pull --ff-only
  Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
  .\build.ps1 -Publish
  ```

  `-Publish` deletes and recreates `dist\`. That fails if the app is still running from `dist\`, so exit it first.

You don't need to update VB-CABLE when you update the app.

## Uninstall

These steps remove what Voice Changer Pro installed. Each one is optional beyond the first two.

1. **Exit the app** from the tray icon (**Exit**). This restores your physical microphone as the Windows default. The uninstaller force-closes a running app, which skips that step.
2. **Remove the auto-start entry**, if you turned on *Start with Windows*. Either switch it off in Settings before uninstalling, or run this afterwards. It only deletes the app's own value:

   ```powershell
   Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'VoiceChangerPro' -ErrorAction SilentlyContinue
   ```

3. **Remove the app:**
   - **Installer:** open *Settings › Apps › Installed apps › Voice Changer Pro by Cyberkyd › Uninstall*, or run:

     ```powershell
     $k = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{7E1C0D6A-4B3F-4A0E-9C55-2F0C6B1D8E21}_is1'
     $u = (Get-ItemProperty "HKCU:\$k", "HKLM:\$k" -ErrorAction SilentlyContinue | Select-Object -First 1).UninstallString
     if ($u) { Start-Process -FilePath ($u -replace '"', '') -Wait } else { 'Voice Changer Pro is not installed with the installer.' }
     ```

     This removes the program folder, the Start menu and desktop shortcuts, and the log folder. An *all users* install asks for administrator rights.
   - **Portable:** delete the file and the folder you created:

     ```powershell
     Remove-Item "$env:USERPROFILE\VoiceChangerPro-Portable" -Recurse -Force
     ```

   - **Source build:** delete the clone (`Remove-Item "$env:USERPROFILE\VoiceChangerPro" -Recurse -Force`). If you installed the tools only for this project, you can remove them with `winget uninstall --id Microsoft.DotNet.SDK.8 -e` and `winget uninstall --id JRSoftware.InnoSetup -e`. If you used the per-user SDK, delete `%LocalAppData%\Microsoft\dotnet`, but only if nothing else uses it.
4. **Delete your settings and presets (optional).** The uninstaller keeps them on purpose. Export any custom presets you want to keep first (right-click › *Export*).

   ```powershell
   Remove-Item "$env:APPDATA\VoiceChangerPro" -Recurse -Force -ErrorAction SilentlyContinue
   Remove-Item "$env:LOCALAPPDATA\VoiceChangerPro" -Recurse -Force -ErrorAction SilentlyContinue
   Remove-Item "$env:TEMP\.net\VoiceChangerPro" -Recurse -Force -ErrorAction SilentlyContinue
   ```

5. **Remove VB-CABLE (optional).** Only do this if no other app uses it, since streaming and calling tools often do. Run the VB-CABLE setup as administrator again, click **Remove Driver**, then restart Windows. If you deleted the folder from step 1, download and unpack the pack again with the first four lines of the manual install commands.

   ```powershell
   Start-Process -FilePath "$env:USERPROFILE\Downloads\VBCABLE\VBCABLE_Setup_x64.exe" -Verb RunAs
   ```

   Afterwards, check *Settings › System › Sound › Input* and pick your microphone if Windows didn't select it automatically.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| *Windows protected your PC* when starting the installer or `.exe` | The binaries aren't code-signed | Check the SHA-256 as shown in step 2, then **More info → Run anyway** |
| Status pill says **No virtual cable** | VB-CABLE isn't installed, or Windows hasn't rebooted since | Do [step 1](#1-install-the-virtual-audio-cable-vb-cable), reboot, press **Refresh**. For another cable product, pick its endpoints in *Settings › Routing* |
| Installer says *VB-CABLE could not be downloaded automatically* | The vendor download was unreachable | Install it manually ([step 1](#1-install-the-virtual-audio-cable-vb-cable)) and reboot |
| **No audio:** IN meter never moves, or *Mic silent — how to fix* appears | Another voice changer's driver (NCH Voxal, MorphVOX/Screaming Bee, AV Voice Changer) is passing silence, or Windows blocks microphone access | Follow the *Mic silent — how to fix* button: usually uninstall that product, restart Windows, then **Refresh**. Also check `Start-Process ms-settings:privacy-microphone`: *Microphone access* and *Let desktop apps access your microphone* must be **On**. Check the headset's mute switch |
| **No audio in the other app:** they hear nothing | That app uses *CABLE Output*, but Voice Changer Pro isn't running, or the mic is switched off in the app | Start Voice Changer Pro and make sure the mic is *Active*. When the app is closed, CABLE Output is silent. If you picked CABLE Output by hand in an app, switch that app back to your real mic when you stop using the voice changer |
| Other apps hear your **real** voice | They aren't using CABLE Output, or effects are off | Select *CABLE Output* in that app (step 4) or click **Set as Windows default mic**, then restart that app. Check the **Voice effects** master switch and the mic's **Effect** switch |
| **Echo:** the other person hears themselves | Your call app's *speaker/output* (or the Windows default playback device) is set to *CABLE Input* | Set the output to your headphones/speakers. Run `control mmsys.cpl`; on the **Playback** tab, *CABLE Input* must not be the default device |
| You hear yourself twice, even with monitoring off | *Listen to this device* is enabled on CABLE Output | `control mmsys.cpl` → **Recording** → *CABLE Output* → Properties → **Listen** → untick *Listen to this device* |
| **Feedback** or howling | Monitoring plays through speakers that your mic picks up | Use headphones, lower the monitor volume, or switch off *Hear myself* |
| **Latency:** your own voice sounds delayed in your ears | The monitoring path, about 150 ms with the default 20 ms buffers (≈ 80 ms of that is the voice processing itself) | Normal. In *Settings › Audio engine* set **Buffer size** to 10 ms (≈ 110 ms total on a fast PC). Avoid Bluetooth mics, which add their own delay. Or turn *Hear myself* off once the voice is set up. The other party isn't affected by your monitoring |
| Crackling or drop-outs | Buffers too small for the PC's load | Raise **Buffer size** to 30–50 ms, close CPU-heavy apps, keep the laptop on mains power |
| **Exclusive-mode conflict:** a mic shows an error / won't start, the output to CABLE Input fails, or a DAW/game loses its device | Voice Changer Pro uses WASAPI **shared** mode. Another app that grabs the same device in **exclusive** mode (common in DAWs and some games) blocks it | `control mmsys.cpl` → **Recording** → your mic → Properties → **Advanced** → untick *Allow applications to take exclusive control of this device* (and *Give exclusive mode applications priority*). Do the same on **Playback** → *CABLE Input*. Or set the other app to shared mode / MME instead of exclusive or ASIO |
| Voice sounds robotic or "double" | Pitch or formant pushed too far | Stay within ±5 semitones of pitch and ±2.5 of formant |
| Build: `running scripts is disabled on this system` | PowerShell execution policy | `Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force`, applies to this window only |
| Build: `dotnet SDK not found. Install .NET 8 SDK or set DOTNET_ROOT.` | The SDK isn't installed or isn't on PATH yet | Install it (step 3.1) and open a **new** PowerShell window. For the per-user SDK, set `$env:DOTNET_ROOT` first |
| Build: `error NETSDK1100 … EnableWindowsTargeting` | You're building on macOS/Linux | Build on Windows 10/11 x64 |
| Build: `Inno Setup 6 not found` | `-Installer` without Inno Setup | `winget install --id JRSoftware.InnoSetup -e` |
| Build: `Run with -Publish first (dist\VoiceChangerPro.exe missing)` | `-Installer` was used on its own | `.\build.ps1 -Publish -Installer` |
| Build: access denied while deleting `dist` | The app is running from `dist\` | Exit it from the tray, then build again |

**Logs:** the status bar's **Logs** button opens `%LocalAppData%\VoiceChangerPro\logs`. Attach the latest
file when you report a problem. More answers are in the [User Guide › Troubleshooting](docs/USER-GUIDE.md#16-troubleshooting).
