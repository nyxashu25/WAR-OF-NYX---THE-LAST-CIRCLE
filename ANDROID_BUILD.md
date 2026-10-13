# Android build guide

Status: settings are applied by **War of Nyx → Setup → Run Project Setup**. Batch build methods exist (Phase 11). **No Android build has been produced yet**: this PC's Unity (6000.6.5f1) has no Android Build Support module (checked 2026-10-13; the build method stops with an explanation).

## Toolchain
- Unity 6.3 LTS with **Android Build Support**, **OpenJDK** and **Android SDK & NDK Tools** (install all three from Unity Hub → Installs → ⚙ → Add modules).
- Before release builds: open **Android SDK Manager** (or Android Studio) and install **Android SDK Platform 36**, so target API "Auto" resolves to 36.

## Settings applied by the setup tool
| Setting | Value |
|---|---|
| Package name | `com.nyxgames.warofnyx` |
| Company / Product | Nyx Games / WAR OF NYX |
| Version / bundle code | 0.1.0 / 1 |
| Orientation | Landscape Left + Right only |
| Scripting backend | IL2CPP |
| Architectures | ARM64 |
| Minimum API | 26 (Android 8.0) |
| Target API | Auto (highest installed). **Must be ≥ 36 for new Google Play submissions in 2026.** Re-check Google's policy before release. |
| Internet permission | Forced (online play and the connectivity probe) |
| Graphics | Unity default (Vulkan + OpenGL ES 3 fallback) |

## Versioning
- `bundleVersion` uses semantic versioning `MAJOR.MINOR.PATCH` (shown in the menu).
- `bundleVersionCode` is an integer that **must go up for every Play upload**. Raise it in Player Settings, or later with a CI script.

## One-click / command-line builds (Phase 11)
- **War of Nyx → Build → Android APK (development)** → `Builds/Android/WarOfNyx-dev.apk` (ARM64, IL2CPP, development build).
- **War of Nyx → Build → Android App Bundle (release, signed)** → `Builds/Android/WarOfNyx.aab`. The upload key comes ONLY from environment variables, and the passwords are cleared from Player Settings right after the build:
  ```bash
  setx WON_KEYSTORE_PATH "%USERPROFILE%\keys\warofnyx-upload.keystore"
  ```
  plus `WON_KEYSTORE_PASS`, `WON_KEY_ALIAS`, `WON_KEY_PASS` (set them in your own terminal; never write them into the project).
- Batch: `Unity.exe -batchmode -projectPath . -executeMethod WarOfNyx.EditorTools.BatchTools.BuildAndroidApk -quit` (exit code 1 with a clear message if the module is missing).

## Build an APK for local testing (manual route)
1. **File → Build Profiles** → select **Android** → **Switch Platform**.
2. Check that the scene list is `Bootstrap` (0), `MainMenu` (1).
3. Turn on **Development Build** for profiling and logs.
4. Connect a phone with USB debugging on, then click **Build And Run**. Or click **Build** to make an `.apk` and install it with:
   ```bash
   adb install -r WarOfNyx.apk
   ```

## Signed App Bundle (Google Play)
1. **Player Settings → Publishing Settings → Keystore Manager** → create a new **upload keystore**. Store it **outside the repository**, e.g. `%USERPROFILE%\keys\warofnyx-upload.keystore`.
2. Back up the keystore and its passwords in a password manager. If you lose them, you lose the ability to update the app unless you use Play App Signing key reset.
3. Enable **Build App Bundle (Google Play)** in Build Profiles, then click Build. Upload the `.aab` to the Play Console internal testing track.
4. Use **Play App Signing**: Google holds the app signing key, and you hold only the upload key.
5. **Never commit** `.keystore` / `.jks` files or passwords. `.gitignore` already blocks these file types.

## Permissions
| Permission | Why |
|---|---|
| `INTERNET` | Online matches, matchmaking, connectivity probe |
| `ACCESS_NETWORK_STATE` | Added by Unity for reachability checks |
No location, contacts, microphone or storage permissions are requested.

## Build verification checklist
- [ ] Launches in landscape. Nothing is covered by notches (safe area).
- [ ] Main menu shows the connection status. With airplane mode on, ONLINE is disabled and OFFLINE still works.
- [ ] Back button returns from sub-screens.
- [ ] App survives background → foreground. Settings persist after restart.
- [ ] Low/Medium/High switch takes effect immediately.
- [ ] No errors in `adb logcat -s Unity`.

## Physical device test plan (Phase 11, needs two Android phones)
Record device model, Android version, RAM, chipset and the graphics tier chosen automatically.
1. Install the development APK on both phones (`adb install -r Builds/Android/WarOfNyx-dev.apk`).
2. Landscape only; notch / punch-hole phones: nothing important under the cutout (safe area).
3. Touch controls: joystick, swipe-to-look, every button; hold FIRE and drag to aim with one finger; gyro aim (Settings → GYRO AIM) in both landscape orientations (if a direction is inverted, report which orientation).
4. In a match: II → EDIT TOUCH CONTROLS → drag buttons, SIZE / OPACITY, RESET, DONE; restart the app: the layout is kept.
5. Graphics LOW / MEDIUM / HIGH: outlines and bloom off on LOW; note FPS (F3 stats on desktop; on device use the Unity Profiler over USB).
6. Offline Ghost Protocol in airplane mode: 11 bots on NYX ISLAND, full match; note FPS and battery temperature after 10 minutes.
7. Online: both phones join the same dedicated server (Phase 12 / local dev server via LAN or Photon Cloud), play a complete match (Phase 11 acceptance).
8. Background/foreground mid-match (home button, phone call), screen lock, low-battery mode.
9. `adb logcat -s Unity` without errors; write device-specific problems into KNOWN_ISSUES.md.
