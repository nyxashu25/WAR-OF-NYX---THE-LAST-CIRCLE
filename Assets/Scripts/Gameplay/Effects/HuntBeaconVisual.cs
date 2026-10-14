using UnityEngine;
using WarOfNyx.App;

namespace WarOfNyx.Gameplay.Effects
{
    /// <summary>
    /// PLACEHOLDER Nyx Hunt exposure beacon (spec 13): a giant crimson light pillar with a purple
    /// energy core, pulsing ground rings and rising purple rings, visible across the map while a
    /// player is exposed for camping. Cheap: three line renderers, no lights, no particles.
    /// Visible to every client (the exposure state is replicated). "Reduce flashing" calms the pulse.
    /// </summary>
    public sealed class HuntBeaconVisual : MonoBehaviour
    {
        private const float Height = 90f;
        private const int RingPoints = 40;

        private static readonly Color Crimson = new Color(1f, 0.19f, 0.31f);
        private static readonly Color Purple = new Color(0.54f, 0.17f, 0.89f);

        private LineRenderer _pillar;
        private LineRenderer _core;
        private LineRenderer _groundRing;
        private LineRenderer _risingRing;
        private bool _active;
        private float _activatedAt;

        private static bool ReduceFlashing => AppServices.IsInitialized && AppServices.UserSettings.Data.reduceFlashing;

        public static HuntBeaconVisual Attach(Transform character)
        {
            var material = Resources.Load<Material>(SpawnEffect.MaterialResource);
            if (material == null || Application.isBatchMode)
                return null;
            return Create(character, material);
        }

        /// <summary>Builds the beacon hidden. Separate from <see cref="Attach"/> so tests can build one in batch mode.</summary>
        public static HuntBeaconVisual Create(Transform character, Material material)
        {
            var go = new GameObject("HuntBeacon (PLACEHOLDER)");
            go.transform.SetParent(character, false);
            var beacon = go.AddComponent<HuntBeaconVisual>();
            beacon.Build(material); // built hidden: _active starts false, so SetActive(false) would be a no-op
            return beacon;
        }

        public void SetActive(bool active)
        {
            if (_active == active)
                return;
            _active = active;
            _activatedAt = Time.time;
            _pillar.enabled = _core.enabled = _groundRing.enabled = _risingRing.enabled = active;
        }

        private void LateUpdate()
        {
            if (!_active)
                return;

            float t = Time.time - _activatedAt;
            bool reduce = ReduceFlashing;
            float pulse = reduce ? 0.85f : 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(t * 4f));
            // Grows in over half a second so it reads as an event, not a pop.
            float grow = Mathf.Clamp01(t * 2f);

            _pillar.widthMultiplier = 2.2f * grow;
            _pillar.startColor = new Color(Crimson.r, Crimson.g, Crimson.b, 0.75f * pulse);
            _pillar.endColor = new Color(Crimson.r, Crimson.g, Crimson.b, 0f);
            _core.widthMultiplier = 0.7f * grow;
            _core.startColor = new Color(Purple.r, Purple.g, Purple.b, 0.9f * pulse);
            _core.endColor = new Color(Purple.r, Purple.g, Purple.b, 0.1f);

            float ring = (t * 0.8f) % 1f;
            _groundRing.transform.localScale = Vector3.one * Mathf.Lerp(1f, 6f, ring);
            SetRing(_groundRing, Crimson, (1f - ring) * 0.9f * pulse);

            float rise = (t * 0.35f) % 1f;
            _risingRing.transform.localPosition = Vector3.up * (rise * Height * 0.6f);
            _risingRing.transform.localScale = Vector3.one * (1.5f + rise);
            SetRing(_risingRing, Purple, (1f - rise) * 0.8f);
        }

        private static void SetRing(LineRenderer line, Color color, float alpha)
        {
            var c = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
            line.startColor = line.endColor = c;
        }

        private void Build(Material material)
        {
            _pillar = Line("Pillar", material, false);
            _pillar.positionCount = 2;
            _pillar.SetPosition(0, Vector3.zero);
            _pillar.SetPosition(1, Vector3.up * Height);

            _core = Line("Core", material, false);
            _core.positionCount = 2;
            _core.SetPosition(0, Vector3.zero);
            _core.SetPosition(1, Vector3.up * Height);

            _groundRing = Ring("GroundRing", material, 0.15f, 0.25f);
            _risingRing = Ring("RisingRing", material, 0.12f, 0f);
        }

        private LineRenderer Ring(string name, Material material, float width, float height)
        {
            var line = Line(name, material, true);
            line.transform.localPosition = Vector3.up * height;
            line.widthMultiplier = width;
            line.positionCount = RingPoints;
            for (int i = 0; i < RingPoints; i++)
            {
                float a = i * Mathf.PI * 2f / RingPoints;
                line.SetPosition(i, new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)));
            }
            return line;
        }

        private LineRenderer Line(string name, Material material, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.loop = loop;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }
    }
}
