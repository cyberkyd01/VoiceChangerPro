# Install Voice Changer Pro on Android

This guide shows you how to install the Android version of Voice Changer Pro by Cyberkyd.
It takes about five minutes. You do not need a computer.

## What the Android app can do

- **Live voice.** Speak into your phone and hear your changed voice in your headphones, right away.
- **Record and share.** Record up to 60 seconds, pick any voice, listen to it, then send the clip
  to WhatsApp, Telegram, Messenger, email or any other app. You can also save it to your Music folder.
- **78 voices.** The same voices as the Windows app: female, male, age, professional, accent and utility voices.
- **Adjust.** Change pitch, formant (voice size), volume, noise reduction and voice isolation.

## What the Android app cannot do

Android does not let any app replace the microphone that other apps use.
So the changed voice is **not** heard live in phone calls, WhatsApp calls, Discord, games or other apps.

To send a changed voice to someone, use **Record**:

1. Open the **Record** tab and record your message.
2. Pick a voice in the **Voices** tab.
3. Tap **Share** and choose the app you want to send it with.

Live mode is for hearing yourself, practising a voice, or playing with friends in the same room.

## What you need

- An Android phone or tablet with **Android 8.0 or newer**.
  Most phones made since 2017 are fine. Very old 32-bit phones are not supported.
- About 60 MB of free space.
- **Wired or USB headphones** for live mode. Bluetooth headphones work too, but they add a delay.

## Step 1. Download the app

1. On your phone, open this direct link in your browser (for example Chrome). It always downloads the newest version:
   **https://github.com/cyberkyd01/VoiceChangerPro/releases/latest/download/VoiceChangerPro-Android.apk**
2. If the browser asks whether to keep or download the file, tap **Download** or **Download anyway**.

You can also open **https://github.com/cyberkyd01/VoiceChangerPro/releases/latest**, scroll down to
**Assets** and tap **VoiceChangerPro-Android.apk**.

The file is an "APK". That is the normal install file for Android apps.

## Step 2. Allow your phone to install the app

Apps that do not come from the Play Store need your permission once.

1. When the download finishes, tap **Open** in the notification or in the browser.
   You can also open the **Files** app (or **My Files** on Samsung), go to **Downloads** and tap the file.
2. Your phone may say: *"For your security, your phone is not allowed to install unknown apps from this source."*
   Tap **Settings**.
3. Turn on **Allow from this source**.
4. Go back (tap the back arrow). The install screen appears again.

The setting only applies to the app you used to open the file (your browser or Files app).
You can turn it off again after the install.

Where to find this setting yourself, if you need it:

- **Most phones:** Settings > Apps > Special app access > Install unknown apps > choose your browser or Files app > Allow from this source.
- **Samsung:** Settings > Apps > the three dots menu > Special access > Install unknown apps.

## Step 3. Install

1. Tap **Install**.
2. **Google Play Protect** may show a warning such as *"Unsafe app blocked"* or *"App scanning recommended"*.
   This happens because the app is not from the Play Store. It does not mean the app is harmful.
   - Tap **More details**, then **Install anyway**.
   - If you are asked to scan the app, you can tap **Scan app**. When the scan finishes, tap **Install**.
3. When it says **App installed**, tap **Open**.

## Step 4. Allow the microphone

1. In the app, tap the big **Start** button on the **Live** tab, or the record button on the **Record** tab.
2. Android asks: *"Allow Voice Changer Pro to record audio?"*
   Tap **While using the app**.

Your voice is processed on your phone only. Nothing is uploaded.

If you tapped **Don't allow** by mistake, the app shows an **Open settings** button.
Or go to Settings > Apps > Voice Changer Pro > Permissions > Microphone > **Allow only while using the app**.

## Step 5. Use it

### Live voice

1. Plug in **wired or USB headphones**.
2. Open the **Voices** tab and tap a voice.
3. Go back to the **Live** tab and tap **Start**. Speak. You hear your changed voice in your headphones.
4. Tap another voice at any time. It changes right away.
5. The **Voice effect** switch turns the effect off, so you hear your normal voice.
6. Tap **Stop** when you are done.

Live mode stops by itself when you leave the app or unplug your headphones.
This protects you from loud howling.

### Record and share

1. Open the **Record** tab and tap the red button. Speak. Tap it again to stop.
2. Tap **Play with voice** to hear the result. Tap **Play original** to compare.
3. Not happy with the voice? Pick another one in **Voices** and play again. You do not need to record again.
4. Tap **Share** and choose WhatsApp, Telegram or any other app.
5. Tap **Save** to keep a copy in **Music > Voice Changer Pro** on your phone.

### Adjust a voice

Open the **Adjust** tab. Move the sliders to change pitch, formant (how big or small the voice sounds),
volume, noise reduction and voice isolation. Tap **Reset to preset** to undo your changes.
The app remembers your voice and settings the next time you open it.

## Troubleshooting

**I hear nothing in live mode**

- Check that your headphones are plugged in all the way.
- Turn up the **media volume** with the volume buttons while live mode is running.
- Check that the app has microphone permission (see Step 4).
- Close other apps that use the microphone, such as voice assistants, call apps or other recorders. Then tap **Start** again.
- Check the **IN** meter on the Live tab. If it does not move when you speak, the phone is not giving the app any sound from the microphone. Restart the phone and try again.

**Loud howling, echo or feedback**

- This happens when the sound comes out of the phone speaker and goes back into the microphone.
- Use headphones. Wired or USB headphones work best.
- If you started without headphones, tap **Stop**, plug in headphones, then tap **Start** again.

**There is a delay between my voice and what I hear**

- A short delay is normal. The voice engine needs a little time to change your voice.
- **Bluetooth headphones add extra delay**, often a quarter of a second or more. This is how Bluetooth works. Use wired or USB headphones for the shortest delay.
- Some phones have more audio delay than others.

**The other person cannot hear my changed voice on a call**

- That is expected. Android does not allow apps to change the microphone for calls or other apps.
  Record a message and use **Share** instead.

**Share or Save does nothing**

- Record a clip first. The buttons are greyed out until you have a recording.
- On Android 8 and 9, **Save** asks for storage permission. Tap **Allow**.

**"App not installed" during install**

- A copy signed differently may already be on your phone, for example one you built yourself. Uninstall Voice Changer Pro first, then install again.
- Make sure there is enough free space.

## Update or remove the app

- **Update:** download the APK again from the direct link in Step 1 and install it over the old one.
  Your voices and settings are kept.
- **Remove:** press and hold the app icon, then tap **Uninstall**.
  Clips you saved in Music > Voice Changer Pro are kept.

## For developers: build from source

The Android app lives in `src/VoiceChanger.Android` and uses the same voice engine as the Windows app
(through `src/VoiceChanger.Shared`). It needs the .NET 10 SDK with the `android` workload,
the Android SDK (API 36) and JDK 17 or 21.

```sh
dotnet workload install android
dotnet build   src/VoiceChanger.Android/VoiceChanger.Android.csproj -c Debug
dotnet publish src/VoiceChanger.Android/VoiceChanger.Android.csproj -c Release
```

Release builds are signed when `signing/voicechangerpro-release.keystore` exists (this folder is not in git).
The password is read from `signing/keystore-password.txt` (the same password on two lines) or from the
`ANDROID_KEYSTORE_PASSWORD` environment variable. Keep the keystore safe: updates must be signed with the same key.
