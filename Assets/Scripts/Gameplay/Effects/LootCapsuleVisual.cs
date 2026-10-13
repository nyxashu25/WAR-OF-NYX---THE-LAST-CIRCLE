using UnityEngine;
using WarOfNyx.Core.Items;
using WarOfNyx.UI.Common;

namespace WarOfNyx.Gameplay.Effects
{
    /// <summary>
    /// PLACEHOLDER holographic loot capsule (spec 14): glowing, rarity-coloured, hovering and spinning,
    /// with a light beam; blinks in its last 10 seconds. Shared by online capsules and offline ones.
    /// No collider: looting is by distance and never blocks shots or movement.
    /// </summary>
    public sealed class LootCapsuleVisual : MonoBehaviour
    {
        private Transform _hologram;
        private Renderer _capsule;
        private LineRenderer _beam;
        private Light _light;
        private Rarity _rarity = (Rarity)255;
        private MaterialPropertyBlock _block;

        public static LootCapsuleVisual Attach(Transform parent)
        {
            if (Application.isBatchMode)
                return null;
            var visual = parent.gameObject.AddComponent<LootCapsuleVisual>();
            visual.Build();
            return visual;
        }

        /// <summary>Call every frame with the current highest rarity and seconds left.</summary>
        public void Show(Rarity highest, float secondsLeft)
        {
            if (_hologram == null)
                return;
            if (_rarity != highest)
            {
                _rarity = highest;
                var color = RarityColors.For(highest);
                _block ??= new MaterialPropertyBlock();
                _block.SetColor("_BaseColor", new Color(color.r, color.g, color.b, 0.55f));
                _block.SetColor("_Color", new Color(color.r, color.g, color.b, 0.55f));
                _capsule.SetPropertyBlock(_block);
                _beam.startColor = new Color(color.r, color.g, color.b, 0.6f);
                _beam.endColor = new Color(color.r, color.g, color.b, 0f);
                _light.color = color;
            }

            float t = Time.time;
            _hologram.localPosition = new Vector3(0f, Mathf.Sin(t * 2f) * 0.12f, 0f);
            _hologram.localRotation = Quaternion.Euler(0f, t * 60f, 0f);
            _light.intensity = secondsLeft < 10f ? (Mathf.Sin(t * 12f) > 0f ? 2f : 0.3f) : 1.5f;
        }

        private void Build()
        {
            var material = Resources.Load<Material>(SpawnEffect.MaterialResource);
            _hologram = new GameObject("Hologram (PLACEHOLDER)").transform;
            _hologram.SetParent(transform, false);

            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(capsule.GetComponent<Collider>());
            capsule.transform.SetParent(_hologram, false);
            capsule.transform.localScale = new Vector3(0.6f, 0.45f, 0.6f);
            _capsule = capsule.GetComponent<Renderer>();
            if (material != null)
                _capsule.sharedMaterial = material;
            _capsule.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var beamGo = new GameObject("Beam");
            beamGo.transform.SetParent(transform, false);
            _beam = beamGo.AddComponent<LineRenderer>();
            if (material != null)
                _beam.sharedMaterial = material;
            _beam.useWorldSpace = false;
            _beam.positionCount = 2;
            _beam.SetPosition(0, Vector3.zero);
            _beam.SetPosition(1, Vector3.up * 8f);
            _beam.widthMultiplier = 0.12f;

            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(transform, false);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.range = 5f;
        }
    }
}
