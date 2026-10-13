using System;
using System.Collections.Generic;
using WarOfNyx.Core.Offline;

namespace WarOfNyx.Core.Bots
{
    /// <summary>Gameplay sounds with hearing radii (spec 38.6). Radii are [T].</summary>
    public enum SoundKind : byte
    {
        Gunshot = 0,
        HeavyGunshot = 1,
        Melee = 2,
        Footsteps = 3,
        Reload = 4,
        Explosion = 5,
    }

    public struct SoundEvent
    {
        public SoundKind Kind;
        public float X;
        public float Z;
        public int SourceId;
        public float Time;
    }

    /// <summary>What a bot needs to know about a candidate target to decide if it can see it.</summary>
    public struct VisibilityQuery
    {
        public float ObserverX, ObserverZ, ObserverYaw;
        public float TargetX, TargetZ;
        public bool TargetCrouched;
        public bool TargetMoving;
        public bool TargetFiring;
        /// <summary>Result of the PHYSICS line-of-sight check (eye to body). Never skipped.</summary>
        public bool LineOfSightClear;
    }

    /// <summary>
    /// Bot senses (spec 38.6). Vision needs distance, field of view AND a clear physics line of
    /// sight; crouching and standing still reduce the range, firing increases it. Very close
    /// targets are noticed from any angle (you can feel someone next to you), still only with line
    /// of sight. Hearing gives an APPROXIMATE position whose error grows with distance.
    /// </summary>
    public static class BotPerception
    {
        public const float ProximityRadius = 4f;
        public const float CrouchVisibility = 0.65f;
        public const float StillVisibility = 0.8f;
        public const float FiringVisibility = 1.5f;

        public static float HearingRadius(SoundKind kind)
        {
            switch (kind)
            {
                case SoundKind.Gunshot: return 60f;
                case SoundKind.HeavyGunshot: return 85f;
                case SoundKind.Melee: return 15f;
                case SoundKind.Footsteps: return 12f;
                case SoundKind.Reload: return 8f;
                case SoundKind.Explosion: return 100f;
                default: return 10f;
            }
        }

        public static float EffectiveVisionRange(BotDifficultyProfile profile, in VisibilityQuery q)
        {
            float range = profile.VisionRangeMeters;
            if (q.TargetCrouched) range *= CrouchVisibility;
            if (!q.TargetMoving) range *= StillVisibility;
            if (q.TargetFiring) range *= FiringVisibility;
            return Math.Min(range, BotDifficultyProfile.MaxVisionRangeMeters);
        }

        public static bool CanSee(BotDifficultyProfile profile, in VisibilityQuery q)
        {
            if (!q.LineOfSightClear)
                return false; // never through walls or terrain
            float dx = q.TargetX - q.ObserverX;
            float dz = q.TargetZ - q.ObserverZ;
            float distance = MathF.Sqrt(dx * dx + dz * dz);
            if (distance <= ProximityRadius)
                return true;
            if (distance > EffectiveVisionRange(profile, q))
                return false;
            float bearing = MathF.Atan2(dx, dz) * (180f / MathF.PI);
            return MathF.Abs(DeltaAngle(q.ObserverYaw, bearing)) <= profile.FieldOfViewDegrees * 0.5f;
        }

        public static bool CanHear(BotDifficultyProfile profile, in SoundEvent sound, float listenerX, float listenerZ)
        {
            float dx = sound.X - listenerX;
            float dz = sound.Z - listenerZ;
            float radius = HearingRadius(sound.Kind) * profile.HearingMultiplier;
            return dx * dx + dz * dz <= radius * radius;
        }

        /// <summary>Where the bot THINKS the sound came from: the true source plus an error up to 25 % of the distance.</summary>
        public static void EstimateSource(in SoundEvent sound, float listenerX, float listenerZ, Random random, out float x, out float z)
        {
            float dx = sound.X - listenerX;
            float dz = sound.Z - listenerZ;
            float distance = MathF.Sqrt(dx * dx + dz * dz);
            float error = distance * 0.25f * (float)random.NextDouble();
            double angle = random.NextDouble() * Math.PI * 2.0;
            x = sound.X + (float)Math.Sin(angle) * error;
            z = sound.Z + (float)Math.Cos(angle) * error;
        }

        public static float DeltaAngle(float from, float to)
        {
            float d = (to - from) % 360f;
            if (d > 180f) d -= 360f;
            if (d < -180f) d += 360f;
            return d;
        }
    }

    /// <summary>One remembered enemy or suspicious position.</summary>
    public sealed class Contact
    {
        public int Id;
        public float X, Z;
        public float VelocityX, VelocityZ;
        public float LastSeen;
        /// <summary>1 = seen right now, decays toward 0; heard-only contacts start lower.</summary>
        public float Confidence;
        public bool Visible;
        /// <summary>When it first became visible in the current sighting (reaction time is measured from here).</summary>
        public float SpottedAt;
        public bool FromSound;
    }

    /// <summary>
    /// Short-term awareness (spec 38.6 "Enemy Memory"): last known position, time, movement
    /// direction, sound sources and a confidence that decays over time. Nothing is ever updated
    /// without a legitimate observation, so a target behind cover is searched for, not tracked.
    /// </summary>
    public sealed class BotMemory
    {
        public const float ConfidenceDecayPerSecond = 0.08f;
        public const float SoundConfidence = 0.45f;
        public const float ForgetBelow = 0.05f;

        private readonly Dictionary<int, Contact> _contacts = new Dictionary<int, Contact>();

        public IEnumerable<Contact> Contacts => _contacts.Values;
        public int Count => _contacts.Count;

        public bool TryGet(int id, out Contact contact) => _contacts.TryGetValue(id, out contact);

        public void See(int id, float x, float z, float now)
        {
            if (!_contacts.TryGetValue(id, out var c))
                _contacts[id] = c = new Contact { Id = id, SpottedAt = now };
            if (!c.Visible)
                c.SpottedAt = now; // a new sighting: the bot needs its reaction time again
            float dt = now - c.LastSeen;
            if (c.Visible && dt > 0.01f)
            {
                c.VelocityX = (x - c.X) / dt;
                c.VelocityZ = (z - c.Z) / dt;
            }
            c.X = x;
            c.Z = z;
            c.LastSeen = now;
            c.Confidence = 1f;
            c.Visible = true;
            c.FromSound = false;
        }

        /// <summary>The target dropped out of sight this update.</summary>
        public void LostSight(int id)
        {
            if (_contacts.TryGetValue(id, out var c))
                c.Visible = false;
        }

        /// <summary>A sound heard at an ESTIMATED position (never the exact source unless seen).</summary>
        public void Hear(int sourceId, float estimatedX, float estimatedZ, float now)
        {
            if (_contacts.TryGetValue(sourceId, out var c) && (c.Visible || c.Confidence >= SoundConfidence))
                return; // already better information
            if (c == null)
                _contacts[sourceId] = c = new Contact { Id = sourceId };
            c.X = estimatedX;
            c.Z = estimatedZ;
            c.VelocityX = c.VelocityZ = 0f;
            c.LastSeen = now;
            c.Confidence = SoundConfidence;
            c.FromSound = true;
        }

        public void Forget(int id) => _contacts.Remove(id);

        /// <summary>Decays confidence of everything not currently visible; forgets stale contacts.</summary>
        public void Decay(float dt)
        {
            List<int> drop = null;
            foreach (var c in _contacts.Values)
            {
                if (c.Visible)
                    continue;
                c.Confidence -= ConfidenceDecayPerSecond * dt;
                if (c.Confidence < ForgetBelow)
                    (drop ??= new List<int>()).Add(c.Id);
            }
            if (drop != null)
                foreach (int id in drop)
                    _contacts.Remove(id);
        }

        /// <summary>Best target: visible beats remembered; then closer; then more recent. No bias toward any player kind.</summary>
        public Contact Best(float x, float z)
        {
            Contact best = null;
            float bestScore = float.NegativeInfinity;
            foreach (var c in _contacts.Values)
            {
                float dx = c.X - x, dz = c.Z - z;
                float distance = MathF.Sqrt(dx * dx + dz * dz);
                float score = (c.Visible ? 100f : 0f) + c.Confidence * 20f - distance * 0.2f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = c;
                }
            }
            return best;
        }

        public Contact BestVisible(float x, float z)
        {
            var best = Best(x, z);
            return best != null && best.Visible ? best : null;
        }
    }
}
