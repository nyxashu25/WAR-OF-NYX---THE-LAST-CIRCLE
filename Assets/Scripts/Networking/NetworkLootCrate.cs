using System.Collections.Generic;
using Fusion;
using UnityEngine;
using WarOfNyx.Combat;
using WarOfNyx.Core.Combat;
using WarOfNyx.Core.Items;
using WarOfNyx.Gameplay.Effects;
using WarOfNyx.UI.Common;

namespace WarOfNyx.Networking
{
    /// <summary>Networked form of one <see cref="LootItem"/>.</summary>
    public struct NetworkLootItem : INetworkStruct
    {
        public byte Kind;
        public short WeaponId;
        public short Magazine;
        public byte Ammo;
        public byte Medkit;
        public short Count;
        public byte Armor;

        public LootItem ToItem() => new LootItem
        {
            Kind = (LootKind)Kind, WeaponId = WeaponId, Magazine = Magazine, Ammo = (AmmoType)Ammo,
            Medkit = (MedkitType)Medkit, Count = Count, ArmorLevel = Armor,
        };

        public static NetworkLootItem From(in LootItem i) => new NetworkLootItem
        {
            Kind = (byte)i.Kind, WeaponId = i.WeaponId, Magazine = (short)i.Magazine, Ammo = (byte)i.Ammo,
            Medkit = (byte)i.Medkit, Count = (short)i.Count, Armor = (byte)i.ArmorLevel,
        };
    }

    /// <summary>
    /// The 12 slots of a capsule as one fixed-layout networked struct (avoids relying on networked
    /// collection syntax). Matches <see cref="LootContainer.Capacity"/>.
    /// </summary>
    public struct NetworkLootItems : INetworkStruct
    {
        public NetworkLootItem I0, I1, I2, I3, I4, I5, I6, I7, I8, I9, I10, I11;

        public NetworkLootItem Get(int index)
        {
            switch (index)
            {
                case 0: return I0; case 1: return I1; case 2: return I2; case 3: return I3;
                case 4: return I4; case 5: return I5; case 6: return I6; case 7: return I7;
                case 8: return I8; case 9: return I9; case 10: return I10; case 11: return I11;
                default: return default;
            }
        }

        public void Set(int index, NetworkLootItem item)
        {
            switch (index)
            {
                case 0: I0 = item; break; case 1: I1 = item; break; case 2: I2 = item; break; case 3: I3 = item; break;
                case 4: I4 = item; break; case 5: I5 = item; break; case 6: I6 = item; break; case 7: I7 = item; break;
                case 8: I8 = item; break; case 9: I9 = item; break; case 10: I10 = item; break; case 11: I11 = item; break;
            }
        }
    }

    /// <summary>
    /// Elimination loot capsule (spec 14). Spawned by the server where a player was eliminated,
    /// holding everything they carried. Everyone can see and open it; only the server changes its
    /// contents (through <see cref="NetworkCombat"/>'s loot request), one validated claim at a time.
    /// Despawns when empty or after <see cref="LifetimeSeconds"/>.
    /// </summary>
    public sealed class NetworkLootCrate : NetworkBehaviour
    {
        public const string PrefabResource = "WarOfNyx/LootCrate";
        public const float LifetimeSeconds = 90f;
        /// <summary>Client prompt distance; the server allows a little extra for latency.</summary>
        public const float InteractRange = 3f;
        public const float ServerRange = 3.75f;

        public static readonly List<NetworkLootCrate> Active = new List<NetworkLootCrate>();

        [Networked] private NetworkLootItems Items { get; set; }
        [Networked] private float ExpiresAt { get; set; }
        [Networked] private byte HighestRarity { get; set; }
        [Networked] public NetworkString<_16> OwnerName { get; set; }

        private Transform _visual;
        private Renderer _capsule;
        private LineRenderer _beam;
        private Light _light;
        private byte _renderedRarity = 255;

        public float SecondsLeft => Runner != null ? Mathf.Max(0f, ExpiresAt - Runner.SimulationTime) : 0f;

        /// <summary>Server: spawn a capsule for an eliminated player's items. Snaps to the ground under the body.</summary>
        public static NetworkLootCrate SpawnFor(NetworkRunner runner, Vector3 deathPosition, LootContainer contents, string ownerName)
        {
            if (contents == null || contents.IsEmpty)
                return null;
            var prefab = Resources.Load<NetworkObject>(PrefabResource);
            if (prefab == null)
            {
                Debug.LogError("[WarOfNyx][Loot] LootCrate prefab missing (Resources/WarOfNyx/LootCrate). Run War of Nyx > Setup.");
                return null;
            }

            var position = deathPosition + Vector3.up * 0.6f;
            if (Physics.Raycast(deathPosition + Vector3.up * 1f, Vector3.down, out var ground, 30f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                position = ground.point + Vector3.up * 0.6f;

            float expiresAt = runner.SimulationTime + LifetimeSeconds;
            var obj = runner.Spawn(prefab, position, Quaternion.identity, null,
                (r, o) => o.GetComponent<NetworkLootCrate>().InitializeServer(contents, ownerName, expiresAt));
            return obj != null ? obj.GetComponent<NetworkLootCrate>() : null;
        }

        private void InitializeServer(LootContainer contents, string ownerName, float expiresAt)
        {
            Store(contents);
            OwnerName = string.IsNullOrEmpty(ownerName) ? "Unknown" : ownerName;
            ExpiresAt = expiresAt;
        }

        public LootItem Get(int index) => Items.Get(index).ToItem();

        public LootContainer ToContainer()
        {
            var c = new LootContainer();
            for (int i = 0; i < LootContainer.Capacity; i++)
                c.Put(i, Get(i));
            return c;
        }

        /// <summary>Server only: write back the contents after a claim.</summary>
        public void Store(LootContainer contents)
        {
            var items = new NetworkLootItems();
            for (int i = 0; i < LootContainer.Capacity; i++)
                items.Set(i, NetworkLootItem.From(contents[i]));
            Items = items;
            HighestRarity = (byte)contents.HighestRarity(WeaponRegistry.Get);
        }

        public override void Spawned()
        {
            Active.Add(this);
            if (!Application.isBatchMode)
                BuildVisual();
        }

        public override void Despawned(NetworkRunner runner, bool hasState) => Active.Remove(this);

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
                return;
            if (Runner.SimulationTime >= ExpiresAt || ToContainer().IsEmpty)
                Runner.Despawn(Object);
        }

        public override void Render()
        {
            if (_visual == null)
                return;

            if (_renderedRarity != HighestRarity)
            {
                _renderedRarity = HighestRarity;
                var color = RarityColors.For((Rarity)HighestRarity);
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", new Color(color.r, color.g, color.b, 0.55f));
                block.SetColor("_Color", new Color(color.r, color.g, color.b, 0.55f));
                _capsule.SetPropertyBlock(block);
                _beam.startColor = new Color(color.r, color.g, color.b, 0.6f);
                _beam.endColor = new Color(color.r, color.g, color.b, 0f);
                _light.color = color;
            }

            // Hover, spin and blink faster in the last 10 seconds.
            float t = Time.time;
            _visual.localPosition = new Vector3(0f, Mathf.Sin(t * 2f) * 0.12f, 0f);
            _visual.localRotation = Quaternion.Euler(0f, t * 60f, 0f);
            bool expiring = SecondsLeft < 10f;
            _light.intensity = expiring ? (Mathf.Sin(t * 12f) > 0f ? 2f : 0.3f) : 1.5f;
        }

        /// <summary>PLACEHOLDER holographic capsule (spec 14): glowing, rarity-coloured, with a light beam.</summary>
        private void BuildVisual()
        {
            var material = Resources.Load<Material>(SpawnEffect.MaterialResource);
            _visual = new GameObject("Hologram (PLACEHOLDER)").transform;
            _visual.SetParent(transform, false);

            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            DestroyImmediate(capsule.GetComponent<Collider>()); // looting is by distance; never blocks shots or movement
            capsule.transform.SetParent(_visual, false);
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
