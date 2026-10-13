# Android build guide

Status: settings are applied by **War of Nyx → Setup → Run Project Setup**. **No Android build has been produced yet.**

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

## Build an APK for local testing
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
