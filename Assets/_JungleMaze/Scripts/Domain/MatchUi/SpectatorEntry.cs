namespace JungleMaze.Domain.MatchUi
{
    /// <summary>
    /// One living player the local spectator may watch (N8). HP and weapon are shown only when the state has them
    /// (Q12): <see cref="HasHp"/> false hides the bar, a null <see cref="WeaponName"/> hides the weapon line.
    /// </summary>
    public readonly struct SpectatorEntry
    {
        public readonly byte NetId;
        public readonly byte Hp;
        public readonly byte MaxHp;
        public readonly bool HasHp;
        public readonly string Name;
        public readonly string WeaponName;

        public SpectatorEntry(byte netId, string name, bool hasHp, byte hp, byte maxHp, string weaponName)
        {
            NetId = netId;
            Name = string.IsNullOrEmpty(name) ? null : name;
            HasHp = hasHp && maxHp > 0;
            Hp = HasHp ? (hp > maxHp ? maxHp : hp) : (byte)0;
            MaxHp = HasHp ? maxHp : (byte)0;
            WeaponName = string.IsNullOrEmpty(weaponName) ? null : weaponName;
        }
    }
}
