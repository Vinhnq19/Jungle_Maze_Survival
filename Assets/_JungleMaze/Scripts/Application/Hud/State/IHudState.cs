using System;
using JungleMaze.Domain.Hud;

namespace JungleMaze.App.Hud.State
{
    /// <summary>
    /// Read port of the HUD state (HUDState): HP, worn gear, held weapon and ammo, match status, Hero pose.
    /// <see cref="Changed"/> fires on the main thread only when a value actually changed; pose has no event.
    /// </summary>
    public interface IHudState
    {
        event Action<HudStateChange> Changed;
        event Action<ItemPickup> ItemPicked;
        event Action<HitDealt> HitDealt;
        event Action<DamageTaken> DamageTaken;
        event Action<DangerChange> DangerChanged;
        int DangerCount { get; }
        DangerWarning GetDangerAt(int index);
        int Hp { get; }
        int MaxHp { get; }
        GearStatus Armor { get; }
        GearStatus Helmet { get; }
        HeldWeaponStatus HeldWeapon { get; }
        AmmoStatus Ammo { get; }
        MatchStatusInfo MatchStatus { get; }
        int MapSizeCells { get; }
        /// <summary>Polled every frame; no event.</summary>
        bool HasPose { get; }
        HeroPose Pose { get; }
    }

    /// <summary>
    /// Write port of the HUD state, used by data owners (WPN-02 HP, ITM-01 gear and held weapon, WPN-01 ammo,
    /// ZON-01 zone countdown, SRV-03 alive count, the Hero pose reporter) and the dev-only mocks. Invalid input is ignored.
    /// </summary>
    public interface IHudStateWriter
    {
        void SetHp(int hp);
        void NotifyPickup(string itemId, int count);
        void NotifyHitDealt(HitTargetKind target);
        void NotifyDamageTaken(bool hasDirection, float directionDegrees);
        void SetDanger(int id, DangerKind kind, float remainingSeconds, bool hasSource, float sourceX, float sourceY);
        void ClearDanger(int id);
        /// <summary>Null/empty id = nothing worn.</summary>
        void SetArmor(string equipmentId, float durability);
        /// <summary>Null/empty id = nothing worn.</summary>
        void SetHelmet(string equipmentId, float durability);
        /// <summary>Null/empty id = bare hands (HAND).</summary>
        void SetHeldWeapon(string weaponId);
        void SetAmmo(int inMagazine, int reserve);
        /// <summary>ZON-01. The HUD shows this value as-is and never counts down on its own (G-15).</summary>
        void SetZoneCountdown(float secondsRemaining);
        /// <summary>SRV-03. <paramref name="total"/> is the number of players at match start (Q13).</summary>
        void SetAliveCount(int alive, int total);
        void SetPose(float x, float y, float rotationDegrees);
        /// <summary>G-7: HP = max, nothing worn, bare hands, no zone countdown or alive count yet.</summary>
        void Reset();
    }
}
