# Known issues & unverified assumptions

| # | Issue | Impact | Plan |
|---|---|---|---|
| 1 | **Phase 1 code has never been compiled.** No Unity Editor or .NET SDK was installed on the machine where it was written. | There may be compile errors on first open. | Open in Unity 6.3, fix errors, record in TESTING.md. |
| 2 | **No `.meta` files committed.** Unity generates them on first import. | GUIDs differ per machine until they are committed. | Commit them right after the first successful open. |
| 3 | **`ProjectSettings/` is mostly absent.** Unity generates defaults, then the setup tool configures them. | Setup must be run once per fresh clone, until the generated settings are committed. | Commit ProjectSettings after setup. |
| 4 | Package versions in `manifest.json` were chosen from registry data, not resolved by a real 6000.3 install. | Package Manager may bump or replace a version. | Accept Unity's suggestion and update DEPENDENCIES.md. |
| 5 | **Fusion 2 support for Unity 6.3 is unconfirmed.** | Could force an editor or SDK version change in Phase 3. | First task of Phase 3. |
| 6 | The setup tool edits `QualitySettings.asset` and `activeInputHandler` through `SerializedObject` (Unity has no public API for these). Field names were taken from Unity 6 YAML. | A step may report FAIL if Unity renamed a field. | The setup dialog lists failed steps. Do those steps by hand (Project Settings → Quality / Player). |
| 7 | URP renderer assets are created from code. Post-processing data is loaded from the URP package path if present. | If missing, post-processing is off on those renderers. | Assign the default PostProcessData on each `URP_*_Renderer` asset if needed. |
| 8 | Main menu is **code-built placeholder UI** using legacy uGUI `Text` and the built-in font. | Looks basic. | Replaced by designed prefabs and TMP in Phase 10. |
| 9 | Offline capacity thresholds (RAM/cores → 5/11/15/23 bots) and the graphics tier mapping are **provisional guesses**. | 23 bots may be allowed on a phone that can't run them, or blocked on one that can. | Replace with measured data in 9G/13. |
| 10 | Bot difficulty numbers are provisional. | Balance. | Tune in 9G with playtests. |
| 11 | Connectivity probe uses `https://www.gstatic.com/generate_204` by default. | It makes an external request every 5 s while the menu is open. | Configurable in GameConfig. Leave empty to use OS reachability only. Revisit for the privacy policy. |
| 12 | Volume (except master), sensitivity, gyro, reduce-shake are **saved but not used** yet. | Changing them has no effect in Phase 1. | Wired up in Phases 2, 10 and 11. |
| 13 | Online, Offline START and Practice buttons only show notices. | Not playable. | Phases 2, 3–4 and 9G. |
| 14 | No Git LFS. | The repo will grow quickly once art and audio land. | Set up LFS before Phase 10 assets (`.gitattributes` lists the binary types). |
