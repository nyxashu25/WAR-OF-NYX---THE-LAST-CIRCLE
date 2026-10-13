using UnityEngine;
using WarOfNyx.App;
using WarOfNyx.Characters;

namespace WarOfNyx.Gameplay.Effects
{
    /// <summary>
    /// Outside-circle look of a character (spec 12): trembling body and weapon, and violet electric
    /// arcs crackling around it. Visible to everyone (the state is replicated). Visual only: the
    /// gameplay instability (movement wobble, aim sway) is applied by the server simulation and is
    /// not affected by these effects or by "Reduce flashing".
    /// </summary>
    public sealed class OutsideZoneVisual : MonoBehaviour
    {
        private const int ArcCount = 2;
        private const int ArcPoints = 7;

        private PlayerAnimationController _animation;
        private LineRenderer[] _arcs;
        private readonly Vector3[] _points = new Vector3[ArcPoints];
        private bool _active;
        private float _nextArcAt;

        private static bool ReduceFlashing => AppServices.IsInitialized && AppServices.UserSettings.Data.reduceFlashing;

        public static OutsideZoneVisual Attach(Transform character)
        {
            if (Application.isBatchMode)
                return null;
            var go = new GameObject("OutsideZoneFX");
            go.transform.SetParent(character, false);
            var visual = go.AddComponent<OutsideZoneVisual>();
            visual.Build(character.GetComponent<PlayerAnimationController>());
            return visual;
        }

        public void SetActive(bool active)
        {
            if (_active == active)
                return;
            _active = active;
            if (!active)
            {
                if (_animation != null)
                    _animation.TrembleOffset = Vector3.zero;
                foreach (var arc in _arcs)
                    arc.enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (!_active)
                return;

            float t = Time.time;
            // Trembling: small, fast, smooth jitter of the body (and the weapon parented to it).
            if (_animation != null)
            {
                _animation.TrembleOffset = new Vector3(
                    (Mathf.PerlinNoise(t * 18f, 0.1f) - 0.5f) * 0.05f,
                    (Mathf.PerlinNoise(0.3f, t * 15f) - 0.5f) * 0.02f,
                    (Mathf.PerlinNoise(t * 16f, 0.7f) - 0.5f) * 0.05f);
            }

            bool reduce = ReduceFlashing;
            if (t >= _nextArcAt)
            {
                _nextArcAt = t + (reduce ? 0.35f : 0.12f);
                foreach (var arc in _arcs)
                    Crackle(arc, reduce);
            }
        }

        private void Crackle(LineRenderer arc, bool reduce)
        {
            float a = Random.value * Mathf.PI * 2f;
            float height = Random.Range(0.4f, 1.7f);
            var start = new Vector3(Mathf.Sin(a) * 0.45f, height, Mathf.Cos(a) * 0.45f);
            var end = new Vector3(Mathf.Sin(a + 1.2f) * 0.5f, height + Random.Range(-0.5f, 0.5f), Mathf.Cos(a + 1.2f) * 0.5f);
            for (int i = 0; i < ArcPoints; i++)
            {
                float f = i / (float)(ArcPoints - 1);
                _points[i] = Vector3.Lerp(start, end, f) + (i == 0 || i == ArcPoints - 1 ? Vector3.zero : Random.insideUnitSphere * 0.12f);
            }
            arc.SetPositions(_points);
            float alpha = reduce ? 0.35f : 0.9f;
            arc.startColor = new Color(0.75f, 0.55f, 1f, alpha);
            arc.endColor = new Color(0.54f, 0.17f, 0.89f, alpha * 0.4f);
            arc.enabled = true;
        }

        private void Build(PlayerAnimationController animation)
        {
            _animation = animation;

            var material = Resources.Load<Material>(SpawnEffect.MaterialResource);
            _arcs = new LineRenderer[ArcCount];
            for (int i = 0; i < ArcCount; i++)
            {
                var go = new GameObject("Arc");
                go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>();
                if (material != null)
                    line.sharedMaterial = material;
                line.useWorldSpace = false;
                line.positionCount = ArcPoints;
                line.widthMultiplier = 0.04f;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.enabled = false;
                _arcs[i] = line;
            }
        }
    }
}
