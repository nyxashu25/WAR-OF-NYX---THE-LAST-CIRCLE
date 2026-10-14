using System.Collections.Generic;
using UnityEngine;

namespace WarOfNyx.Gameplay.World
{
    /// <summary>Marks a place where cherry petals drift (placed near Crimson Temple by the map builder).</summary>
    public sealed class PetalSource : MonoBehaviour
    {
        [SerializeField] private float radius = 120f;
        private static readonly List<PetalSource> All = new List<PetalSource>();

        public float Radius => radius;

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        public static PetalSource Nearest(Vector3 position, out float distance)
        {
            PetalSource best = null;
            distance = float.MaxValue;
            foreach (var source in All)
            {
                float d = Vector3.Distance(position, source.transform.position);
                if (d < distance)
                {
                    distance = d;
                    best = source;
                }
            }
            return best;
        }
    }
}
