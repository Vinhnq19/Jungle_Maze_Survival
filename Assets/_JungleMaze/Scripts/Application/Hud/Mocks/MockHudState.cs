#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using JungleMaze.App.Hud.State;
using JungleMaze.App.Ports;
using JungleMaze.Domain.Balance;
using JungleMaze.Domain.Hud;

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
        private readonly List<string> _weaponIds = new List<string>();
        private readonly List<string> _armorIds = new List<string>();
        private readonly List<string> _helmetIds = new List<string>();

        public MockHudState(IHudStateWriter writer, IBalanceConfigProvider balance)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            foreach (var weapon in balance.Weapons) _weaponIds.Add(weapon.Id);
            foreach (var equipment in balance.Equipments)
            {
                if (equipment.Type == EquipmentType.Armor) _armorIds.Add(equipment.Id);
                else if (equipment.Type == EquipmentType.Helmet) _helmetIds.Add(equipment.Id);
            }
            Hp = MaxHp;
        }

        /// <summary>Weapon ids in weapons.json order (for the F4 panel).</summary>
        public IReadOnlyList<string> WeaponIds => _weaponIds;
        /// <summary>Armor ids in equipments.json order.</summary>
        public IReadOnlyList<string> ArmorIds => _armorIds;
        /// <summary>Helmet ids in equipments.json order.</summary>
        public IReadOnlyList<string> HelmetIds => _helmetIds;

        /// <summary>durabilityPoints of a helmet/armor id, 0 when unknown (for the "full durability" button).</summary>
        public float MaxDurability(string equipmentId) =>
            equipmentId != null && _balance.TryGetEquipment(equipmentId, out var equipment) ? equipment.Stats.DurabilityPoints ?? 0f : 0f;

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
        public void Pickup(string id,int count) => _writer.NotifyPickup(id,count);
        public void Hit(HitTargetKind target) => _writer.NotifyHitDealt(target);
        public void Damage(float? degrees) => _writer.NotifyDamageTaken(degrees.HasValue,degrees??0);
        public void Bomb(int id,float fuse,float x,float y) => _writer.SetDanger(id,DangerKind.FusedBomb,fuse,true,x,y);
        public void Trap(int id,float x,float y) => _writer.SetDanger(id,DangerKind.Trap,0,true,x,y);
        public void ClearDanger(int id) => _writer.ClearDanger(id);

        public void Reset()
        {
            Hp = MaxHp;
            _writer.Reset();
        }
    }
}
#endif
