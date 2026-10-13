using Fusion;
using WarOfNyx.Core.Combat;

namespace WarOfNyx.Networking
{
    /// <summary>Networked copy of <see cref="WeaponState"/> (server-authoritative, rolled back for prediction).</summary>
    public struct NetworkWeaponSlot : INetworkStruct
    {
        public short WeaponId;
        public short Magazine;
        public short Reserve;
        public float NextFireAt;
        public float ReloadEndsAt;
        public byte BurstRemaining;
        public float NextBurstShotAt;
        public float Bloom;
        public float BloomUpdatedAt;
        public int ShotsFired;

        public WeaponState ToState() => new WeaponState
        {
            WeaponId = WeaponId,
            Magazine = Magazine,
            Reserve = Reserve,
            NextFireAt = NextFireAt,
            ReloadEndsAt = ReloadEndsAt,
            BurstRemaining = BurstRemaining,
            NextBurstShotAt = NextBurstShotAt,
            Bloom = Bloom,
            BloomUpdatedAt = BloomUpdatedAt,
            ShotsFired = ShotsFired,
        };

        public static NetworkWeaponSlot From(in WeaponState s) => new NetworkWeaponSlot
        {
            WeaponId = s.WeaponId,
            Magazine = (short)s.Magazine,
            Reserve = (short)s.Reserve,
            NextFireAt = s.NextFireAt,
            ReloadEndsAt = s.ReloadEndsAt,
            BurstRemaining = (byte)s.BurstRemaining,
            NextBurstShotAt = s.NextBurstShotAt,
            Bloom = s.Bloom,
            BloomUpdatedAt = s.BloomUpdatedAt,
            ShotsFired = s.ShotsFired,
        };
    }
}
