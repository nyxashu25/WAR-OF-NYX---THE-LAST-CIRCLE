using System;
using System.Collections.Generic;

namespace WarOfNyx.Core.Combat
{
    /// <summary>One tick of combat intent (client → server). Edges are true only on the press tick.</summary>
    public struct CombatCommand
    {
        public bool FireHeld;
        public bool FirePressed;
        public bool ReloadPressed;
        public bool MeleePressed;
        /// <summary>0 = none, 1..3 = select that slot.</summary>
        public int SelectSlot;
        public bool NextWeaponPressed;
        /// <summary>Use a medkit (the server picks the best fitting one).</summary>
        public bool MedkitPressed;
    }

    /// <summary>
    /// The three weapon slots (spec 18: 1/2/3): 0 = primary ranged, 1 = secondary ranged, 2 = melee.
    /// Phase 5 grants a fixed development loadout; Phase 6 replaces it with randomized,
    /// power-budgeted starting loadouts and loot.
    /// </summary>
    public static class CombatLoadout
    {
        public const int SlotCount = 3;
        public const int MeleeSlot = 2;
        /// <summary>Weapon swap time before the new weapon can fire.</summary>
        public const float SwitchSeconds = 0.3f;

        public static readonly string[] DevelopmentLoadout =
        {
            WeaponCatalog.Nx7AssaultRifle, WeaponCatalog.NeonPistol, WeaponCatalog.Katana,
        };

        public static WeaponState[] CreateDevelopmentSlots()
        {
            var slots = new WeaponState[SlotCount];
            for (int i = 0; i < SlotCount; i++)
                slots[i] = WeaponState.Create(WeaponCatalog.NetworkIdOf(DevelopmentLoadout[i]));
            return slots;
        }

        /// <summary>Slot to switch to for a command, or -1 to stay. Empty slots are skipped.</summary>
        public static int ResolveSwitch(int currentSlot, in CombatCommand cmd, Func<int, bool> slotHasWeapon)
        {
            if (cmd.SelectSlot >= 1 && cmd.SelectSlot <= SlotCount)
            {
                int target = cmd.SelectSlot - 1;
                return target != currentSlot && slotHasWeapon(target) ? target : -1;
            }

            if (cmd.NextWeaponPressed)
            {
                for (int step = 1; step < SlotCount; step++)
                {
                    int candidate = (currentSlot + step) % SlotCount;
                    if (slotHasWeapon(candidate))
                        return candidate;
                }
            }

            return -1;
        }
    }

    /// <summary>Kill feed text (spec 19 "Live Match Feed").</summary>
    public static class CombatText
    {
        public static string Elimination(string killer, string victim, string weapon, bool headshot)
        {
            if (string.IsNullOrEmpty(killer) || killer == victim)
                return $"{victim} was eliminated";
            return $"{killer} eliminated {victim} with {weapon}{(headshot ? " (HEADSHOT)" : string.Empty)}";
        }
    }

    /// <summary>
    /// Drops duplicate network events (spec 19: "Provide event IDs ... to prevent duplicate
    /// network announcements"). Remembers the most recent <see cref="Capacity"/> ids.
    /// </summary>
    public sealed class EventDeduplicator
    {
        public const int Capacity = 256;
        private readonly HashSet<int> _seen = new HashSet<int>();
        private readonly Queue<int> _order = new Queue<int>();

        /// <summary>True the first time an id is seen; false for repeats.</summary>
        public bool TryAccept(int eventId)
        {
            if (!_seen.Add(eventId))
                return false;
            _order.Enqueue(eventId);
            if (_order.Count > Capacity)
                _seen.Remove(_order.Dequeue());
            return true;
        }
    }
}
