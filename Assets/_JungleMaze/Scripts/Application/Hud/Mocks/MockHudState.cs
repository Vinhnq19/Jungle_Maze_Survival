#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using JungleMaze.App.Hud.State;
using JungleMaze.App.Ports;

namespace JungleMaze.App.Hud.Mocks
{
    /// <summary>
    /// Dev-only stand-in for the HUD state owners (G-8, AC16). Writes through <see cref="IHudStateWriter"/> exactly like
    /// a real owner would, so it works next to real sources. Keeps its own copy of HP for Damage/Heal steps.
    /// Not compiled into release builds.
    /// </summary>
    public sealed class MockHudState
    {
        private readonly IHudStateWriter _writer;
        private readonly IBalanceConfigProvider _balance;

        public MockHudState(IHudStateWriter writer, IBalanceConfigProvider balance)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            Hp = MaxHp;
        }

        public int MaxHp => _balance.Match.HeroMaxHp;
        /// <summary>Last HP this mock wrote.</summary>
        public int Hp { get; private set; }

        public void SetHp(int hp)
        {
            Hp = hp;
            _writer.SetHp(hp);
        }

        public void Damage(int amount) => SetHp(Math.Max(0, Hp - amount));
        public void Heal(int amount) => SetHp(Math.Min(MaxHp, Hp + amount));
        public void EquipArmor(string equipmentId, float durability) => _writer.SetArmor(equipmentId, durability);
        public void EquipHelmet(string equipmentId, float durability) => _writer.SetHelmet(equipmentId, durability);
        public void Hold(string weaponId) => _writer.SetHeldWeapon(weaponId);
        public void SetAmmo(int inMagazine, int reserve) => _writer.SetAmmo(inMagazine, reserve);

        /// <summary>Holds <paramref name="weaponId"/> with a full magazine and full reserve from weapons.json.</summary>
        public void FullMagazine(string weaponId)
        {
            _writer.SetHeldWeapon(weaponId);
            if (_balance.TryGetWeapon(weaponId, out var weapon))
                _writer.SetAmmo(weapon.Stats.MagazineSize ?? 0, weapon.Stats.ReserveAmmo ?? 0);
        }

        public void Zone(float secondsRemaining) => _writer.SetZoneCountdown(secondsRemaining);
        public void Alive(int alive, int total) => _writer.SetAliveCount(alive, total);
        public void Pose(float x, float y, float rotationDegrees) => _writer.SetPose(x, y, rotationDegrees);

        public void Reset()
        {
            Hp = MaxHp;
            _writer.Reset();
        }
    }
}
#endif
