using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using WarOfNyx.App;
using WarOfNyx.Core.Weather;
using WarOfNyx.Gameplay.Offline;

namespace WarOfNyx.Gameplay.World
{
    /// <summary>
    /// Developer screenshot run of the real game (not the editor), for checking how the island looks
    /// in a built player: grass, weather, shadows, HUD. Started with
    ///   WarOfNyx.exe -devCapture &lt;folder&gt; [-screen-width 1600 -screen-height 900 -screen-fullscreen 0]
    /// It starts an OFFLINE NYX ISLAND match (fixed seed, 11 bots), captures the player's view under
    /// several times of day / weathers and a few fixed viewpoints, then quits. Offline only: it never
    /// touches online play, accounts or results (offline results are never ranked anyway).
    /// </summary>
    public sealed class DevCapture : MonoBehaviour
    {
        private string _folder;
        private string _only = "all";
        private int _index;

        /// <summary>Starts a capture run when <c>-devCapture</c> is on the command line.</summary>
        public static bool TryStart(string[] args)
        {
            string folder = null;
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-devCapture")
                    folder = args[i + 1];
            if (string.IsNullOrEmpty(folder))
                return false;
            var go = new GameObject("DevCapture");
            DontDestroyOnLoad(go);
            var capture = go.AddComponent<DevCapture>();
            capture._folder = folder;
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-devCaptureOnly")
                    capture._only = args[i + 1]; // world | characters | all
            return true;
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(_folder);
            Debug.Log($"[WarOfNyx][DevCapture] Writing screenshots to {Path.GetFullPath(_folder)}");
            if (_only == "menu")
            {
                yield return MenuShowcase();
                Debug.Log($"[WarOfNyx][DevCapture] Done: {_index} screenshots.");
                Application.Quit(0);
                yield break;
            }
            OfflineMatchRequest.Reset();
            OfflineMatchRequest.Bots = 11;
            OfflineMatchRequest.Seed = 20261014;
            yield return SceneManager.LoadSceneAsync(AppServices.Config.nyxIslandScene, LoadSceneMode.Single);
            yield return new WaitForSeconds(5f);

            var weather = WeatherSystem.Current;
            if (_only == "places")
            {
                weather?.Begin(new AtmospherePlan { Time = TimeOfDay.Day, Start = WeatherKind.Clear, Later = WeatherKind.Clear, ChangeAt = float.PositiveInfinity });
                var placesCam = Camera.main;
                var placesFollow = placesCam != null ? placesCam.GetComponent<ThirdPersonCamera>() : null;
                if (placesFollow != null)
                    placesFollow.enabled = false;
                if (placesCam != null)
                {
                    yield return View(placesCam, 160f, -230f, 230f, -300f, 1.7f, "view_meadow");
                    yield return View(placesCam, -265f, 172f, -265f, 270f, 1.8f, "view_neon_street");
                    yield return View(placesCam, -265f, 196f, -282f, 240f, 15f, "view_neon_rooftops");
                    yield return View(placesCam, 280f, 168f, 280f, 238f, 3f, "view_temple_approach");
                    yield return View(placesCam, 330f, -185f, 330f, -110f, 3f, "view_fortress_gate");
                    yield return View(placesCam, 30f, -415f, -10f, -470f, 5f, "view_docks");
                    yield return View(placesCam, 34f, -48f, 0f, 0f, 4f, "view_core");
                    weather?.Begin(new AtmospherePlan { Time = TimeOfDay.Night, Start = WeatherKind.Clear, Later = WeatherKind.Clear, ChangeAt = float.PositiveInfinity });
                    yield return new WaitForSeconds(0.5f);
                    yield return View(placesCam, -265f, 172f, -265f, 270f, 1.8f, "view_neon_street_night");
                    yield return View(placesCam, 280f, 168f, 280f, 238f, 3f, "view_temple_night");
                }
                Debug.Log($"[WarOfNyx][DevCapture] Done: {_index} screenshots.");
                Application.Quit(0);
                yield break;
            }
            if (_only == "blood")
            {
                weather?.Begin(new AtmospherePlan { Time = TimeOfDay.Day, Start = WeatherKind.Clear, Later = WeatherKind.Clear, ChangeAt = float.PositiveInfinity });
                yield return BloodShowcase();
                Debug.Log($"[WarOfNyx][DevCapture] Done: {_index} screenshots.");
                Application.Quit(0);
                yield break;
            }
            if (_only == "arsenal")
            {
                weather?.Begin(new AtmospherePlan { Time = TimeOfDay.Day, Start = WeatherKind.Clear, Later = WeatherKind.Clear, ChangeAt = float.PositiveInfinity });
                yield return ArsenalShowcase();
                Debug.Log($"[WarOfNyx][DevCapture] Done: {_index} screenshots.");
                Application.Quit(0);
                yield break;
            }
            if (_only == "vehicles")
            {
                weather?.Begin(new AtmospherePlan { Time = TimeOfDay.Day, Start = WeatherKind.Clear, Later = WeatherKind.Clear, ChangeAt = float.PositiveInfinity });
                yield return VehicleShowcase();
                Debug.Log($"[WarOfNyx][DevCapture] Done: {_index} screenshots.");
                Application.Quit(0);
                yield break;
            }
            if (_only == "roster")
            {
                weather?.Begin(new AtmospherePlan { Time = TimeOfDay.Day, Start = WeatherKind.Clear, Later = WeatherKind.Clear, ChangeAt = float.PositiveInfinity });
                yield return RosterShowcase();
                Debug.Log($"[WarOfNyx][DevCapture] Done: {_index} screenshots.");
                Application.Quit(0);
                yield break;
            }
            if (_only == "freelook")
            {
                weather?.Begin(new AtmospherePlan { Time = TimeOfDay.Day, Start = WeatherKind.Clear, Later = WeatherKind.Clear, ChangeAt = float.PositiveInfinity });
                yield return FreeLookShowcase();
                Debug.Log($"[WarOfNyx][DevCapture] Done: {_index} screenshots.");
                Application.Quit(0);
                yield break;
            }
            if (_only == "characters")
            {
                weather?.Begin(new AtmospherePlan { Time = TimeOfDay.Day, Start = WeatherKind.Clear, Later = WeatherKind.Clear, ChangeAt = float.PositiveInfinity });
                yield return CharacterShowcase();
                Debug.Log($"[WarOfNyx][DevCapture] Done: {_index} screenshots.");
                Application.Quit(0);
                yield break;
            }
            if (weather != null)
            {
                foreach (var (time, kind) in new[]
                         {
                             (TimeOfDay.Day, WeatherKind.Clear), (TimeOfDay.Dusk, WeatherKind.Clear), (TimeOfDay.Night, WeatherKind.Clear),
                             (TimeOfDay.Dawn, WeatherKind.Fog), (TimeOfDay.Day, WeatherKind.Rain), (TimeOfDay.Dusk, WeatherKind.Storm),
                         })
                {
                    weather.Begin(new AtmospherePlan { Time = time, Start = kind, Later = kind, ChangeAt = float.PositiveInfinity });
                    yield return new WaitForSeconds(2.5f); // let rain fill the air
                    yield return Capture($"player_{time}_{kind}");
                }
                weather.Begin(new AtmospherePlan { Time = TimeOfDay.Day, Start = WeatherKind.Clear, Later = WeatherKind.Clear, ChangeAt = float.PositiveInfinity });
            }

            // Fixed viewpoints (the follow camera is switched off).
            var cam = Camera.main;
            if (cam != null)
            {
                var follow = cam.GetComponent<ThirdPersonCamera>();
                if (follow != null)
                    follow.enabled = false;
                yield return View(cam, 160f, -230f, 230f, -300f, 1.7f, "view_meadow");
                yield return View(cam, -260f, -95f, -330f, -120f, 1.7f, "view_shadow_forest");
                yield return View(cam, 255f, 150f, 280f, 240f, 2f, "view_crimson_temple");
                yield return View(cam, -60f, 230f, 20f, 330f, 2.5f, "view_eclipse_valley");
                // Buildings and props (detail pass).
                yield return View(cam, -265f, 172f, -265f, 270f, 1.8f, "view_neon_street");
                yield return View(cam, -265f, 196f, -282f, 240f, 15f, "view_neon_rooftops");
                yield return View(cam, 280f, 168f, 280f, 238f, 3f, "view_temple_approach");
                yield return View(cam, 330f, -185f, 330f, -110f, 3f, "view_fortress_gate");
                yield return View(cam, 30f, -415f, -10f, -470f, 5f, "view_docks");
                yield return View(cam, 34f, -48f, 0f, 0f, 4f, "view_core");
                if (weather != null)
                {
                    weather.Begin(new AtmospherePlan { Time = TimeOfDay.Night, Start = WeatherKind.Clear, Later = WeatherKind.Clear, ChangeAt = float.PositiveInfinity });
                    yield return new WaitForSeconds(0.5f);
                    yield return View(cam, -265f, 172f, -265f, 270f, 1.8f, "view_neon_street_night");
                    yield return View(cam, 280f, 168f, 280f, 238f, 3f, "view_temple_night");
                    weather.Begin(new AtmospherePlan { Time = TimeOfDay.Day, Start = WeatherKind.Clear, Later = WeatherKind.Clear, ChangeAt = float.PositiveInfinity });
                }
            }
            if (_only == "all")
                yield return CharacterShowcase();
            Debug.Log($"[WarOfNyx][DevCapture] Done: {_index} screenshots.");
            Application.Quit(0);
        }

        /// <summary>Screen blood: a critical hit, a second hit (badly hurt), then healing clears it.</summary>
        private IEnumerator BloodShowcase()
        {
            var match = OfflineMatch.Instance;
            var human = match != null ? match.Human : null;
            if (human == null)
                yield break;
            OfflineCombatant attacker = null;
            foreach (var c in FindObjectsByTypeSafe<OfflineCombatant>())
                if (c != human)
                    attacker = c;
            if (attacker == null)
                yield break;
            human.Protection = default; // spawn protection would block the test hits
            yield return Capture("blood_0_unhurt");
            match.ApplyWeaponDamage(attacker, human, 35f, true, human.Slots[0].WeaponId);
            yield return new WaitForSeconds(0.4f);
            Debug.Log($"[WarOfNyx][DevCapture] after headshot: health {human.Health.Health:0}");
            yield return Capture("blood_1_headshot");
            match.ApplyWeaponDamage(attacker, human, 40f, false, human.Slots[0].WeaponId);
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[WarOfNyx][DevCapture] badly hurt: health {human.Health.Health:0}");
            yield return Capture("blood_2_badly_hurt");
            Core.Combat.HealthRules.Heal(ref human.Health, 75f);
            yield return new WaitForSeconds(1.6f);
            Debug.Log($"[WarOfNyx][DevCapture] healed: health {human.Health.Health:0}");
            yield return Capture("blood_3_healed");
        }

        /// <summary>The main menu's CHARACTERS screen for every roster character.</summary>
        private IEnumerator MenuShowcase()
        {
            yield return SceneManager.LoadSceneAsync(AppServices.Config.mainMenuScene, LoadSceneMode.Single);
            yield return new WaitForSeconds(1.5f);
            var menu = FindObjectsByTypeSafe<UI.MainMenu.MainMenuController>();
            if (menu.Length == 0)
                yield break;
            yield return Capture("menu_home");
            menu[0].ShowCharacters();
            for (int i = 0; i < WarOfNyx.Characters.CharacterRoster.Count; i++)
            {
                yield return new WaitForSeconds(1.2f);
                yield return Capture($"menu_characters_{WarOfNyx.Characters.CharacterRoster.Get(i).Name}");
                menu[0].BrowseCharacter(1);
            }
        }

        /// <summary>Every roster character close up: front, three-quarter, back, face, and running (cloth in motion).</summary>
        private IEnumerator RosterShowcase()
        {
            var cam = Camera.main;
            if (cam == null)
                yield break;
            var follow = cam.GetComponent<ThirdPersonCamera>();
            if (follow != null)
                follow.enabled = false;
            foreach (var c in FindObjectsByTypeSafe<Offline.OfflineCombatant>())
                c.gameObject.SetActive(false);
            var start = new Vector3(-30f, Ground(-30f, -60f), -60f);
            float fov = cam.fieldOfView;
            for (int id = 0; id < WarOfNyx.Characters.CharacterRoster.Count; id++)
            {
                var look = WarOfNyx.Characters.CharacterRoster.Get(id);
                var (root, rig) = Puppet(id, look.SignatureWeapon, start);
                cam.fieldOfView = 32f;
                yield return new WaitForSeconds(0.6f);
                foreach (var (label, offset, height) in new[]
                         {
                             ("front", new Vector3(0f, 0f, 3.2f), 1.0f), ("three_quarter", new Vector3(2.3f, 0f, 2.3f), 1.0f),
                             ("back", new Vector3(0.6f, 0f, -3.2f), 1.0f), ("face", new Vector3(0.25f, 0f, 1.0f), 1.6f),
                         })
                {
                    cam.transform.position = root.position + offset + Vector3.up * (height + (label == "face" ? 0.05f : 0.15f));
                    cam.transform.LookAt(root.position + Vector3.up * height);
                    yield return new WaitForSeconds(0.25f);
                    yield return Capture($"roster_{look.Name}_{label}");
                }
                // Running: cloth trails behind.
                float t = 0f;
                while (t < 1.4f)
                {
                    t += Time.deltaTime;
                    root.position += Vector3.forward * 4.4f * Time.deltaTime;
                    root.position = new Vector3(root.position.x, Ground(root.position.x, root.position.z), root.position.z);
                    cam.transform.position = root.position + new Vector3(2.6f, 1.2f, 0.6f);
                    cam.transform.LookAt(root.position + Vector3.up * 1.0f);
                    yield return null;
                }
                yield return Capture($"roster_{look.Name}_running");
                Destroy(root.gameObject);
            }
            cam.fieldOfView = fov;
        }

        /// <summary>The EYE: normal view, holding free look turned ~150° (looking behind), and the view after release.</summary>
        private IEnumerator FreeLookShowcase()
        {
            var human = Offline.OfflineMatch.Instance != null ? Offline.OfflineMatch.Instance.Human : null;
            var router = human != null ? human.Router : null;
            if (router == null)
                yield break;
            yield return new WaitForSeconds(1f);
            yield return Capture("freelook_0_normal");
            router.SetTouchFreeLook(true);
            for (int i = 0; i < 30; i++)
            {
                // ~150° to the right in total, through the same path as a swipe.
                router.AddTouchLook(new Vector2(150f / 30f / 180f * Screen.height * 1f, 0f));
                yield return null;
            }
            yield return new WaitForSeconds(0.2f);
            Debug.Log($"[WarOfNyx][DevCapture] free look offset {router.FreeLookYaw:0}°, aim yaw {router.Yaw:0}°, camera yaw {router.CameraYaw:0}°");
            yield return Capture("freelook_1_behind");
            router.SetTouchFreeLook(false);
            yield return new WaitForSeconds(0.6f);
            Debug.Log($"[WarOfNyx][DevCapture] after release: free look offset {router.FreeLookYaw:0}°");
            yield return Capture("freelook_2_released");
        }

        // ------------------------------------------------------------------ arsenal and vehicles

        /// <summary>Every firearm and the war axe side-on in close-up, then two characters holding new weapons.</summary>
        private IEnumerator ArsenalShowcase()
        {
            var cam = Camera.main;
            if (cam == null)
                yield break;
            var follow = cam.GetComponent<ThirdPersonCamera>();
            if (follow != null)
                follow.enabled = false;
            foreach (var c in FindObjectsByTypeSafe<Offline.OfflineCombatant>())
                c.gameObject.SetActive(false);
            var stage = new Vector3(-30f, Ground(-30f, -60f) + 1.4f, -60f);
            foreach (var w in Core.Combat.WeaponCatalog.All)
            {
                if (w.IsMelee && w.id != Core.Combat.WeaponCatalog.WarAxe)
                    continue;
                var holder = new GameObject("Arsenal " + w.id).transform;
                holder.position = stage;
                holder.rotation = Quaternion.Euler(0f, 90f, w.IsMelee ? 90f : 0f);
                Effects.WeaponModelFactory.Build(w, holder, out _);
                float length = w.IsMelee ? 0.8f : w.id == Core.Combat.WeaponCatalog.ArcSniper ? 1.6f : w.id == Core.Combat.WeaponCatalog.NeonPistol || w.id == Core.Combat.WeaponCatalog.ViperRevolver ? 0.5f : 1.1f;
                var centre = stage + Vector3.right * (w.IsMelee ? 0f : length * 0.35f);
                cam.transform.position = centre + new Vector3(0.15f, 0.12f, -length * 0.95f);
                cam.transform.LookAt(centre);
                yield return new WaitForSeconds(0.25f);
                yield return Capture("arsenal_" + w.id);
                Destroy(holder.gameObject);
            }
            foreach (var (look, weaponId) in new[] { (1, Core.Combat.WeaponCatalog.VortexLmg), (3, Core.Combat.WeaponCatalog.TalonAutoShotgun),
                         (2, Core.Combat.WeaponCatalog.ArcSniper), (0, Core.Combat.WeaponCatalog.WarAxe) })
            {
                var (root, rig) = Puppet(look, weaponId, stage - Vector3.up * 1.4f);
                rig?.SetAiming(true);
                Frame(cam, root, 2.6f, frontish: true);
                yield return new WaitForSeconds(0.6f);
                yield return Capture($"arsenal_held_{weaponId}");
                Destroy(root.gameObject);
            }
        }

        /// <summary>
        /// The three vehicles parked (detail views), then the human drives a buggy (chase camera, speed log), runs over a
        /// bot placed in its path (health log), gets out, and a truck is destroyed (explosion and wreck).
        /// </summary>
        private IEnumerator VehicleShowcase()
        {
            var match = Offline.OfflineMatch.Instance;
            var cam = Camera.main;
            if (match == null || cam == null || match.Human == null)
                yield break;
            Debug.Log($"[WarOfNyx][DevCapture] vehicles parked on the map: {match.Vehicles.Count}, supply caches: {match.Crates.Count}");
            var follow = cam.GetComponent<ThirdPersonCamera>();
            var human = match.Human;
            human.Protection = default;
            while (!match.Unlocked)
                yield return null;

            // Parked showcase on open ground near the human.
            if (follow != null)
                follow.enabled = false;
            var basePos = human.Feet + human.transform.forward * 12f;
            var shown = new System.Collections.Generic.List<Offline.OfflineVehicle>();
            for (int type = 0; type < Core.Vehicles.VehicleCatalog.All.Length; type++)
            {
                var spot = basePos + human.transform.right * (type - 1) * 7f;
                var v = match.SpawnVehicle(type, type, new Vector3(spot.x, Ground(spot.x, spot.z), spot.z), human.Yaw + 30f);
                shown.Add(v);
            }
            yield return new WaitForSeconds(2f);
            foreach (var v in shown)
            {
                var c = v.transform.position + Vector3.up * 0.9f;
                foreach (var (label, offset) in new[] { ("front", new Vector3(3.2f, 1.2f, 5.2f)), ("rear", new Vector3(-3.6f, 1.6f, -5.4f)), ("detail", new Vector3(1.8f, 0.5f, 1.6f)) })
                {
                    cam.transform.position = c + v.transform.rotation * offset;
                    cam.transform.LookAt(c + (label == "detail" ? v.transform.rotation * new Vector3(0.9f, -0.3f, 1.2f) : Vector3.zero));
                    yield return new WaitForSeconds(0.3f);
                    yield return Capture($"vehicle_{v.Stats.Id}_{label}");
                }
            }

            // Drive the buggy with the chase camera.
            var buggy = shown[0];
            if (follow != null)
                follow.enabled = true;
            // Walk up to the driver's door (the authority checks the distance).
            human.Movement.Teleport(buggy.ExitPosition(0), buggy.Yaw);
            yield return new WaitForSeconds(0.2f);
            string reason = match.TryEnterVehicle(human, buggy);
            Debug.Log($"[WarOfNyx][DevCapture] enter buggy: {(reason ?? "ok")}, seat {human.Seat}");
            yield return new WaitForSeconds(0.6f);
            yield return Capture("drive_0_seated");
            // A bot stands 30 m ahead in the buggy's path.
            Offline.OfflineCombatant target = null;
            foreach (var c in match.Combatants)
                if (c != human && c.Alive)
                {
                    target = c;
                    break;
                }
            if (target != null)
            {
                var ahead = buggy.transform.position + buggy.transform.forward * 30f;
                target.Movement.Teleport(new Vector3(ahead.x, Ground(ahead.x, ahead.z) + 0.1f, ahead.z), 0f);
                target.Protection = default;
            }
            float health0 = target != null ? target.Health.Health : 0f;
            match.DevDriveInput = new Vector2(0f, 1f);
            for (int i = 0; i < 4; i++)
            {
                yield return new WaitForSeconds(0.9f);
                Debug.Log($"[WarOfNyx][DevCapture] driving: {buggy.Kmh} km/h, grounded {buggy.Grounded}");
                yield return Capture($"drive_{i + 1}_speed_{buggy.Kmh}kmh");
            }
            // Hold the target still 14 m ahead of the moving buggy (bots dodge) and drive through it.
            if (target != null && target.Alive)
            {
                health0 = target.Health.Health;
                var hold = buggy.transform.position + buggy.transform.forward * 14f;
                hold.y = Ground(hold.x, hold.z) + 0.05f;
                for (float t = 0f; t < 2f && target.Alive && target.Health.Health >= health0; t += Time.deltaTime)
                {
                    target.Movement.Teleport(hold, 0f);
                    yield return null;
                }
                yield return Capture("drive_4b_run_over");
                Debug.Log($"[WarOfNyx][DevCapture] run-over at {buggy.Kmh} km/h: target health {health0:0} -> {target.Health.Health:0}, alive {target.Alive}");
            }
            match.DevDriveInput = new Vector2(0.8f, 0.7f);
            yield return new WaitForSeconds(1.2f);
            yield return Capture("drive_5_turning");
            match.DevDriveInput = new Vector2(0f, -1f);
            yield return new WaitForSeconds(2.5f);
            match.DevDriveInput = null;
            Debug.Log($"[WarOfNyx][DevCapture] braked to {buggy.Kmh} km/h");
            reason = match.TryExitVehicle(human);
            Debug.Log($"[WarOfNyx][DevCapture] exit: {(reason ?? "ok")}, in vehicle {human.Vehicle != null}, controller {human.Movement.Controller.enabled}");
            yield return new WaitForSeconds(0.8f);
            yield return Capture("drive_6_got_out");

            // Destroy the truck (damage from the human's gun, as if shot to pieces).
            var truck = shown[1];
            if (follow != null)
                follow.enabled = false;
            var tc = truck.transform.position + Vector3.up;
            cam.transform.position = tc + new Vector3(9f, 4f, 9f);
            cam.transform.LookAt(tc);
            match.ApplyVehicleDamage(human, truck, truck.Health - 1f);
            Debug.Log($"[WarOfNyx][DevCapture] truck health {truck.Health:0}, destroyed {truck.Destroyed}");
            match.ApplyVehicleDamage(human, truck, 50f);
            Debug.Log($"[WarOfNyx][DevCapture] truck health {truck.Health:0}, destroyed {truck.Destroyed}");
            yield return new WaitForSeconds(0.12f);
            yield return Capture("explosion_0");
            yield return new WaitForSeconds(0.35f);
            yield return Capture("explosion_1");
            yield return new WaitForSeconds(2.5f);
            yield return Capture("explosion_2_wreck");
        }

        // ------------------------------------------------------------------ character showcase

        /// <summary>A display character (styled body, animator, weapon rig) with no gameplay components.</summary>
        private static (Transform root, Effects.WeaponVisualRig rig) Puppet(int look, string weaponId, Vector3 position)
        {
            var root = new GameObject("Showcase puppet").transform;
            root.position = position;
            var visual = new GameObject("Visual (PLACEHOLDER)").transform;
            visual.SetParent(root, false);
            Effects.CharacterStyler.Apply(visual, WarOfNyx.Characters.CharacterRoster.Get(look));
            var rig = Effects.WeaponVisualRig.Attach(root, visual);
            rig?.ShowWeapon(Core.Combat.WeaponCatalog.Get(weaponId));
            return (root, rig);
        }

        /// <summary>
        /// Gaits and combat poses on display characters on the core plaza, filmed side-on: idle / walk / run /
        /// sprint with a rifle, running with a katana, aiming, reloading, and every katana combo attack.
        /// </summary>
        private IEnumerator CharacterShowcase()
        {
            var cam = Camera.main;
            if (cam == null)
                yield break;
            var follow = cam.GetComponent<ThirdPersonCamera>();
            if (follow != null)
                follow.enabled = false;
            // Clear the stage: hide the match's characters so only the puppet is filmed.
            foreach (var c in FindObjectsByTypeSafe<Offline.OfflineCombatant>())
                c.gameObject.SetActive(false);

            var start = new Vector3(-30f, Ground(-30f, -60f), -60f);
            foreach (var (label, speed, weapon, aim) in new[]
                     {
                         ("idle_rifle", 0f, Core.Combat.WeaponCatalog.Nx7AssaultRifle, false),
                         ("walk_rifle", 1.8f, Core.Combat.WeaponCatalog.Nx7AssaultRifle, false),
                         ("run_rifle", 4.2f, Core.Combat.WeaponCatalog.Nx7AssaultRifle, false),
                         ("sprint_rifle", 6.6f, Core.Combat.WeaponCatalog.Nx7AssaultRifle, false),
                         ("aim_walk_rifle", 1.6f, Core.Combat.WeaponCatalog.Nx7AssaultRifle, true),
                         ("run_katana", 4.2f, Core.Combat.WeaponCatalog.Katana, false),
                         ("sprint_katana", 6.6f, Core.Combat.WeaponCatalog.Katana, false),
                         ("run_pistol", 4.2f, Core.Combat.WeaponCatalog.NeonPistol, false),
                     })
            {
                var (root, rig) = Puppet(_index % 4, weapon, start);
                rig?.SetAiming(aim);
                float t = 0f;
                int shot = 0;
                while (shot < 3)
                {
                    float dt = Time.deltaTime;
                    t += dt;
                    root.position += Vector3.forward * speed * dt;
                    root.position = new Vector3(root.position.x, Ground(root.position.x, root.position.z), root.position.z);
                    var side = root.position + new Vector3(3.6f, 1.1f, 0.4f);
                    cam.transform.position = side;
                    cam.transform.LookAt(root.position + Vector3.up * 0.95f);
                    if (t > 1.2f + shot * 0.13f)
                    {
                        yield return Capture($"char_{label}_{shot}");
                        shot++;
                    }
                    else
                    {
                        yield return null;
                    }
                }
                Destroy(root.gameObject);
            }

            // Reload and the katana / energy blade combos, standing.
            {
                var (root, rig) = Puppet(1, Core.Combat.WeaponCatalog.Nx7AssaultRifle, start);
                Frame(cam, root, 3.2f);
                yield return new WaitForSeconds(0.5f);
                rig?.PlayReload();
                yield return new WaitForSeconds(0.45f);
                yield return Capture("char_reload");
                Destroy(root.gameObject);
            }
            foreach (var weaponId in new[] { Core.Combat.WeaponCatalog.Katana, Core.Combat.WeaponCatalog.EnergyBlade })
            {
                var weaponStats = Core.Combat.WeaponCatalog.Get(weaponId);
                var combo = Core.Combat.MeleeRules.Combo(weaponStats);
                for (int step = 0; step < combo.Length; step++)
                {
                    var (root, rig) = Puppet(2, weaponId, start);
                    Frame(cam, root, 3.4f, frontish: true);
                    yield return new WaitForSeconds(0.4f);
                    rig?.PlaySwing(combo[step], weaponStats);
                    float windup = weaponStats.meleeWindup * combo[step].WindupMultiplier;
                    yield return new WaitForSeconds(windup * 0.9f);
                    yield return Capture($"char_{weaponId}_{combo[step].Pattern}_a_windup");
                    yield return new WaitForSeconds(windup * 0.1f + 0.07f);
                    yield return Capture($"char_{weaponId}_{combo[step].Pattern}_b_strike");
                    yield return new WaitForSeconds(0.1f);
                    yield return Capture($"char_{weaponId}_{combo[step].Pattern}_c_follow");
                    Destroy(root.gameObject);
                }
            }
        }

        private static void Frame(Camera cam, Transform target, float distance, bool frontish = false)
        {
            var offset = frontish ? new Vector3(distance * 0.75f, 1.2f, distance * 0.65f) : new Vector3(distance, 1.1f, 0.4f);
            cam.transform.position = target.position + offset;
            cam.transform.LookAt(target.position + Vector3.up * 1f);
        }

        private static T[] FindObjectsByTypeSafe<T>() where T : Component
        {
            var list = new System.Collections.Generic.List<T>();
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                list.AddRange(root.GetComponentsInChildren<T>(true));
            return list.ToArray();
        }

        private IEnumerator View(Camera cam, float x, float z, float tx, float tz, float eye, string name)
        {
            cam.transform.position = new Vector3(x, Ground(x, z) + eye, z);
            cam.transform.LookAt(new Vector3(tx, Ground(tx, tz) + 1.2f, tz));
            yield return new WaitForSeconds(0.5f);
            yield return Capture(name);
        }

        /// <summary>24-bit bottom-up BMP from RGB24 rows (Unity's ReadPixels rows are bottom-up too).</summary>
        private static void WriteBmp(string path, int width, int height, byte[] rgb)
        {
            int stride = (width * 3 + 3) & ~3;
            int size = 54 + stride * height;
            using var stream = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(stream);
            w.Write((byte)'B'); w.Write((byte)'M'); w.Write(size); w.Write(0); w.Write(54);
            w.Write(40); w.Write(width); w.Write(height); w.Write((short)1); w.Write((short)24);
            w.Write(0); w.Write(stride * height); w.Write(2835); w.Write(2835); w.Write(0); w.Write(0);
            var row = new byte[stride];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = (y * width + x) * 3;
                    row[x * 3] = rgb[i + 2];     // B
                    row[x * 3 + 1] = rgb[i + 1]; // G
                    row[x * 3 + 2] = rgb[i];     // R
                }
                w.Write(row);
            }
        }

        private static float Ground(float x, float z) =>
            Physics.Raycast(new Vector3(x, 300f, z), Vector3.down, out var hit, 600f, ~(1 << 2 | World.EnvironmentLayers.FoliageMask), QueryTriggerInteraction.Ignore)
                ? hit.point.y
                : 6f;

        private IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(_folder, $"{++_index:00}_{name}.bmp");
            // Read the back buffer and write an uncompressed BMP: the ScreenCapture / ImageConversion
            // modules aren't part of the game, and a developer tool shouldn't add them.
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            texture.Apply();
            WriteBmp(path, texture.width, texture.height, texture.GetRawTextureData());
            Destroy(texture);
            Debug.Log($"[WarOfNyx][DevCapture] {path}");
        }
    }
}
