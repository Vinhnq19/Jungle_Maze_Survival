namespace JungleMaze.Domain.Hud
{
    /// <summary>
    /// Local player values shown by the HUD outside inventory and effects: HP, worn gear, held weapon, ammo,
    /// match status and pose. It only stores values pushed by their owners; it never derives them. Every setter
    /// returns the change flags (None when the value is identical or invalid) and never throws.
    /// </summary>
    public sealed class HudStatus
    {
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }
        public GearStatus Armor { get; private set; }
        public GearStatus Helmet { get; private set; }
        public HeldWeaponStatus HeldWeapon { get; private set; }
        public AmmoStatus Ammo { get; private set; }
        public MatchStatusInfo MatchStatus { get; private set; }
        public bool HasPose { get; private set; }
        public HeroPose Pose { get; private set; }

        public HudStateChange SetHp(int hp)
        {
            if (hp == Hp) return HudStateChange.None;
            Hp = hp;
            return HudStateChange.Hp;
        }

        public HudStateChange SetArmor(in GearStatus gear)
        {
            if (float.IsNaN(gear.Durability) || gear.Equals(Armor)) return HudStateChange.None;
            Armor = gear;
            return HudStateChange.Armor;
        }

        public HudStateChange SetHelmet(in GearStatus gear)
        {
            if (float.IsNaN(gear.Durability) || gear.Equals(Helmet)) return HudStateChange.None;
            Helmet = gear;
            return HudStateChange.Helmet;
        }

        public HudStateChange SetHeldWeapon(in HeldWeaponStatus weapon)
        {
            if (weapon.Equals(HeldWeapon)) return HudStateChange.None;
            HeldWeapon = weapon;
            return HudStateChange.HeldWeapon;
        }

        public HudStateChange SetAmmo(int inMagazine, int reserve)
        {
            if (inMagazine == Ammo.InMagazine && reserve == Ammo.Reserve) return HudStateChange.None;
            Ammo = new AmmoStatus(inMagazine, reserve);
            return HudStateChange.Ammo;
        }

        public HudStateChange SetZoneCountdown(float secondsRemaining)
        {
            if (float.IsNaN(secondsRemaining)) return HudStateChange.None;
            var current = MatchStatus;
            if (current.HasZoneCountdown && current.ZoneSecondsRemaining == secondsRemaining) return HudStateChange.None;
            MatchStatus = new MatchStatusInfo(true, secondsRemaining, current.HasAlive, current.Alive, current.Total);
            return HudStateChange.ZoneCountdown;
        }

        public HudStateChange SetAliveCount(int alive, int total)
        {
            var current = MatchStatus;
            if (current.HasAlive && current.Alive == alive && current.Total == total) return HudStateChange.None;
            MatchStatus = new MatchStatusInfo(current.HasZoneCountdown, current.ZoneSecondsRemaining, true, alive, total);
            return HudStateChange.AliveCount;
        }

        /// <summary>Continuous data: stored without a change flag; readers poll it every frame. NaN is ignored.</summary>
        public void SetPose(float x, float y, float rotationDegrees)
        {
            if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(rotationDegrees)) return;
            Pose = new HeroPose(x, y, rotationDegrees);
            HasPose = true;
        }

        /// <summary>Start-of-match state (G-7): full HP, nothing worn, bare hands, no ammo/match status/pose.</summary>
        public HudStateChange Reset(int maxHp, in HeldWeaponStatus bareHands)
        {
            MaxHp = maxHp;
            Hp = maxHp;
            Armor = default;
            Helmet = default;
            HeldWeapon = bareHands;
            Ammo = default;
            MatchStatus = default;
            HasPose = false;
            Pose = default;
            return HudStateChange.All;
        }
    }
}
