using UnityEngine;

namespace WarOfNyx.Gameplay.Effects
{
    /// <summary>
    /// Translucent cyan bubble shown while a player's spawn protection is active, so everyone can
    /// see who can't be damaged yet. Visual only; protection itself is server state.
    /// </summary>
    public sealed class SpawnShieldVisual : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private Renderer _renderer;
        private MaterialPropertyBlock _block;
        private bool _visible;

        public static SpawnShieldVisual Attach(Transform character)
        {
            var material = Resources.Load<Material>(SpawnEffect.MaterialResource);
            if (material == null || Application.isBatchMode)
                return null;

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "SpawnShield";
            Object.Destroy(sphere.GetComponent<Collider>()); // never affects physics or hit detection
            sphere.transform.SetParent(character, false);
            sphere.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            sphere.transform.localScale = new Vector3(1.5f, 2.1f, 1.5f);

            var shield = sphere.AddComponent<SpawnShieldVisual>();
            shield._renderer = sphere.GetComponent<Renderer>();
            shield._renderer.sharedMaterial = material;
            shield._renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shield._block = new MaterialPropertyBlock();
            shield.SetVisible(false);
            return shield;
        }

        public void SetVisible(bool visible)
        {
            if (_visible == visible && _renderer.enabled == visible)
                return;
            _visible = visible;
            _renderer.enabled = visible;
        }

        private void LateUpdate()
        {
            if (!_visible)
                return;
            float pulse = 0.18f + 0.08f * Mathf.Sin(Time.time * 6f);
            var color = new Color(0f, 0.9f, 1f, pulse);
            _block.SetColor(BaseColorId, color);
            _block.SetColor(ColorId, color);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
