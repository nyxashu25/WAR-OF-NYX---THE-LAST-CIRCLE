using Fusion;
using WarOfNyx.Core.Combat;

namespace WarOfNyx.Networking
{
    /// <summary>Networked copy of <see cref="WeaponState"/> (server-authoritative, rolled back for prediction).</summary>
    public struct NetworkWeaponSlot : INetworkStruct
    {
        public short WeaponId;
        public short Magazine;
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

namespace WarOfNyx.Networking
{
    /// <summary>Networked copy of <see cref="WarOfNyx.Core.Items.PlayerInventory"/> (ammo by type, medkits, armor).</summary>
    public struct NetworkInventory : INetworkStruct
    {
        public short LightAmmo;
        public short RifleAmmo;
        public short ShellAmmo;
        public short MarksmanAmmo;
        public byte SmallMedkits;
        public byte StandardMedkits;
        public byte AdvancedMedkits;
        public byte ArmorLevel;

        public WarOfNyx.Core.Items.PlayerInventory ToInventory() => new WarOfNyx.Core.Items.PlayerInventory
        {
            LightAmmo = LightAmmo, RifleAmmo = RifleAmmo, ShellAmmo = ShellAmmo, MarksmanAmmo = MarksmanAmmo,
            SmallMedkits = SmallMedkits, StandardMedkits = StandardMedkits, AdvancedMedkits = AdvancedMedkits,
            ArmorLevel = ArmorLevel,
        };

        public static NetworkInventory From(in WarOfNyx.Core.Items.PlayerInventory i) => new NetworkInventory
        {
            LightAmmo = (short)i.LightAmmo, RifleAmmo = (short)i.RifleAmmo, ShellAmmo = (short)i.ShellAmmo,
            MarksmanAmmo = (short)i.MarksmanAmmo, SmallMedkits = (byte)i.SmallMedkits, StandardMedkits = (byte)i.StandardMedkits,
            AdvancedMedkits = (byte)i.AdvancedMedkits, ArmorLevel = (byte)i.ArmorLevel,
        };
    }
}
