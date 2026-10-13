using UnityEngine;
using WarOfNyx.Core.Networking;

namespace WarOfNyx.Gameplay
{
    /// <summary>
    /// TEMPORARY Phase 3 spawn layout for networked test sessions: players are placed on a ring
    /// around this transform (see <see cref="SpawnRing"/>). Used only by the authority.
    /// Phase 4 replaces it with the real server-side random spawn system (spec 6).
    /// </summary>
    public sealed class ArenaSpawnArea : MonoBehaviour
    {
        [SerializeField] private float radius = 12f;
        [SerializeField] private int slots = 12;
        [SerializeField] private float spawnHeight = 0.2f;

        public static ArenaSpawnArea Current { get; private set; }

        private void OnEnable() => Current = this;

        private void OnDisable()
        {
            if (Current == this)
                Current = null;
        }

        public void GetSpawn(int playerIndex, out Vector3 position, out float yaw)
        {
            SpawnRing.Slot(playerIndex, slots, radius, out float x, out float z, out yaw);
            position = transform.position + new Vector3(x, spawnHeight, z);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.78f, 0.34f);
            for (int i = 0; i < slots; i++)
            {
                GetSpawn(i, out var p, out _);
                Gizmos.DrawWireSphere(p + Vector3.up * 0.9f, 0.4f);
            }
        }
    }
}
