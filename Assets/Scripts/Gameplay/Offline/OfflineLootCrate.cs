using UnityEngine;
using WarOfNyx.Combat;
using WarOfNyx.Core.Items;
using WarOfNyx.Gameplay.Effects;

namespace WarOfNyx.Gameplay.Offline
{
    /// <summary>
    /// Offline elimination capsule (spec 14, 38.13): the same <see cref="LootContainer"/> rules as
    /// online (atomic take, items moved never copied, 90 s lifetime, gone when empty), owned by the
    /// local match. Humans and bots take items through the same match method.
    /// </summary>
    public sealed class OfflineLootCrate : MonoBehaviour
    {
        public const float LifetimeSeconds = 90f;
        public const float InteractRange = 3f;

        private LootCapsuleVisual _visual;

        public LootContainer Contents { get; private set; }
        public string OwnerName { get; private set; } = string.Empty;
        public float ExpiresAt { get; private set; }
        public float SecondsLeft(float now) => Mathf.Max(0f, ExpiresAt - now);

        /// <summary>A supply cache placed on the map at the start (never expires; gone when emptied).</summary>
        public bool IsCache { get; private set; }

        public static OfflineLootCrate SpawnCache(Vector3 ground, LootContainer contents)
        {
            var crate = Spawn(ground + Vector3.up * 0.3f, contents, "Supply cache", 0f);
            if (crate != null)
            {
                crate.IsCache = true;
                crate.ExpiresAt = float.PositiveInfinity;
                crate.gameObject.name = "SupplyCache";
            }
            return crate;
        }

        public static OfflineLootCrate Spawn(Vector3 deathPosition, LootContainer contents, string ownerName, float now)
        {
            if (contents == null || contents.IsEmpty)
                return null;
            var position = deathPosition + Vector3.up * 0.6f;
            if (Physics.Raycast(deathPosition + Vector3.up, Vector3.down, out var ground, 30f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                position = ground.point + Vector3.up * 0.6f;

            var go = new GameObject("LootCapsule " + ownerName);
            go.transform.position = position;
            var crate = go.AddComponent<OfflineLootCrate>();
            crate.Contents = contents;
            crate.OwnerName = ownerName;
            crate.ExpiresAt = now + LifetimeSeconds;
            crate._visual = LootCapsuleVisual.Attach(go.transform);
            return crate;
        }

        public void Render(float now)
        {
            if (_visual != null && Contents != null)
                _visual.Show(Contents.HighestRarity(WeaponRegistry.Get), SecondsLeft(now));
        }
    }
}
