namespace JungleMaze.Domain.Audio
{
    /// <summary>The 26 gameplay sound effects of AUD-01 (spec table 2.1, same order). UI sounds are not part of this list.</summary>
    public enum SfxId : byte
    {
        None = 0,
        FootstepGrass = 1,
        FootstepSwamp,
        BushEnter,
        MeleeSwing,
        MeleeHit,
        ShootRanged,
        ShootCrossbow,
        Throw,
        Reload,
        EmptyClick,
        BombFuse,
        Explosion,
        TrapTriggered,
        HitMarker,
        HurtLocal,
        ArmorAbsorb,
        Eliminated,
        PickupWeapon,
        PickupItem,
        WearArmor,
        UseHeal,
        UseDrink,
        UseCancel,
        ZoneBell3,
        DragonRoar,
        MatchEnd
    }

    /// <summary>Mixer bus a sound plays on. The user only sets Master, Sfx and Music; Alert follows Sfx.</summary>
    public enum AudioBus : byte
    {
        Master = 0,
        Sfx = 1,
        Music = 2,
        Alert = 3
    }

    /// <summary>Voice priority when the pool is full. A sound only ever evicts a strictly lower priority.</summary>
    public enum SfxPriority : byte
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    /// <summary>Helpers over <see cref="SfxId"/>.</summary>
    public static class SfxIds
    {
        /// <summary>Number of real ids (None excluded).</summary>
        public const int Count = 26;

        /// <summary>Array length for tables indexed by <c>(int)SfxId</c>, including the None slot.</summary>
        public const int TableSize = Count + 1;

        public static bool IsValid(SfxId id) => id > SfxId.None && (int)id <= Count;
    }
}
