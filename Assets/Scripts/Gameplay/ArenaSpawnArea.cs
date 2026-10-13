using UnityEngine;
using UnityEngine.AI;
using WarOfNyx.Core.Match;

namespace WarOfNyx.Gameplay
{
    /// <summary>
    /// The map's starting safe zone for spawning (centre = this transform, <see cref="zoneRadius"/>).
    /// Used only by the authority: it runs <see cref="SpawnPlanner"/> with a physics/navmesh
    /// validator so every spawn is on walkable, open, dry, navigable ground (spec 6).
    /// From Phase 7 the radius comes from the first Nyx Circle phase.
    /// </summary>
    public sealed class ArenaSpawnArea : MonoBehaviour, ISpawnValidator
    {
        [Tooltip("Radius of the starting safe zone (m). Spawns stay inside it, minus the edge margin.")]
        [SerializeField] private float zoneRadius = 55f;

        [Header("Validation")]
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private LayerMask blockingMask = ~0;
        [SerializeField] private string waterLayer = "Water";
        [SerializeField, Range(0f, 60f)] private float maxSlopeDegrees = 35f;
        [SerializeField] private float capsuleRadius = 0.45f;
        [SerializeField] private float capsuleHeight = 1.9f;
        [Tooltip("Reject points with a roof above them (inside buildings) unless interiors are marked valid later.")]
        [SerializeField] private bool rejectCovered = true;
        [SerializeField] private float coverCheckHeight = 25f;
        [SerializeField] private float navMeshTolerance = 0.75f;
        [Tooltip("With a NavMesh, require a walkable path from the zone centre (rejects isolated rooftops and sealed rooms).")]
        [SerializeField] private bool requireReachable = true;

        private bool _navMeshChecked;
        private bool _hasNavMesh;
        private bool _centreResolved;
        private bool _hasCentre;
        private Vector3 _navCentre;
        private NavMeshPath _path;

        public static ArenaSpawnArea Current { get; private set; }
        public float ZoneRadius => zoneRadius;

        private void OnEnable() => Current = this;

        private void OnDisable()
        {
            if (Current == this)
                Current = null;
        }

        public SpawnPlan PlanSpawns(int playerCount, MatchRules rules, int seed)
        {
            var c = transform.position;
            var plan = SpawnPlanner.Plan(playerCount, c.x, c.z, zoneRadius, rules, this, seed);
            if (plan.Success && plan.Relaxed)
                Debug.LogWarning($"[WarOfNyx][Spawn] Separation relaxed to {plan.Separation} m for {playerCount} players (zone {zoneRadius} m).");
            return plan;
        }

        public bool TryValidate(float x, float z, out float groundY)
        {
            groundY = 0f;
            var top = new Vector3(x, transform.position.y + 200f, z);
            if (!Physics.Raycast(top, Vector3.down, out var hit, 400f, groundMask, QueryTriggerInteraction.Ignore))
                return false; // nothing to stand on (off the map)

            int water = LayerMask.NameToLayer(waterLayer);
            if (water >= 0 && hit.collider.gameObject.layer == water)
                return false;

            if (Vector3.Angle(hit.normal, Vector3.up) > maxSlopeDegrees)
                return false;

            var feet = hit.point;

            if (!OnNavMesh(feet) || !ReachableFromCentre(feet))
                return false;

            // Room for the capsule: no walls, props or other geometry overlapping it.
            var bottom = feet + Vector3.up * (capsuleRadius + 0.05f);
            var upper = feet + Vector3.up * (capsuleHeight - capsuleRadius);
            if (Physics.CheckCapsule(bottom, upper, capsuleRadius, blockingMask, QueryTriggerInteraction.Ignore))
                return false;

            // Covered = inside a building / under a structure.
            if (rejectCovered && Physics.Raycast(feet + Vector3.up * capsuleHeight, Vector3.up, coverCheckHeight, blockingMask, QueryTriggerInteraction.Ignore))
                return false;

            groundY = feet.y;
            return true;
        }

        private bool OnNavMesh(Vector3 feet)
        {
            if (!_navMeshChecked)
            {
                _navMeshChecked = true;
                _hasNavMesh = NavMesh.CalculateTriangulation().vertices.Length > 0;
                if (!_hasNavMesh)
                    Debug.LogWarning("[WarOfNyx][Spawn] No baked NavMesh in this scene: spawn validation uses physics only. " +
                                     "Rebuild the scene with War of Nyx > Setup to bake one.");
            }

            if (!_hasNavMesh)
                return true;
            return NavMesh.SamplePosition(feet, out var navHit, navMeshTolerance, NavMesh.AllAreas)
                   && (navHit.position - feet).sqrMagnitude <= navMeshTolerance * navMeshTolerance;
        }

        private bool ReachableFromCentre(Vector3 feet)
        {
            if (!_hasNavMesh || !requireReachable)
                return true;

            if (!_centreResolved)
            {
                _centreResolved = true;
                _hasCentre = NavMesh.SamplePosition(transform.position, out var centreHit, 15f, NavMesh.AllAreas);
                _navCentre = centreHit.position;
                _path = new NavMeshPath();
                if (!_hasCentre)
                    Debug.LogWarning("[WarOfNyx][Spawn] Zone centre is not on the NavMesh; reachability check skipped.");
            }

            if (!_hasCentre)
                return true;
            return NavMesh.CalculatePath(_navCentre, feet, NavMesh.AllAreas, _path) && _path.status == NavMeshPathStatus.PathComplete;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.78f, 0.34f);
            const int segments = 64;
            var c = transform.position;
            var prev = c + new Vector3(0f, 0.2f, zoneRadius);
            for (int i = 1; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var next = c + new Vector3(Mathf.Sin(a) * zoneRadius, 0.2f, Mathf.Cos(a) * zoneRadius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
