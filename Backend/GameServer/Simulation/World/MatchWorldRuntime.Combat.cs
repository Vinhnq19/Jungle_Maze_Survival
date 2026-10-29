using GameServer.Simulation.Combat;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.World;
using JungleMaze.World.Inventory;

namespace GameServer.Simulation;

public sealed partial class MatchWorldRuntime : IEffectService, GameServer.Simulation.Combat.IItemPassive, IBreakableObstacles, ICombatLoadout
{
    private uint _confirmedWorldAction;
    private uint _combatTick;
    public static string WeaponItemId(CombatWeaponId weapon) => weapon switch
    {
        CombatWeaponId.Hand => "HAND", CombatWeaponId.TreeBranch => "TREE_BRANCH", CombatWeaponId.Knife => "KNIFE",
        CombatWeaponId.Bone => "BONE", CombatWeaponId.Axe => "AXE", CombatWeaponId.Mace => "MACE",
        CombatWeaponId.RubberGun => "RUBBER_GUN", CombatWeaponId.Scepter => "SCEPTER",
        CombatWeaponId.Crossbow => "CROSSBOW", CombatWeaponId.Shotgun => "SHOTGUN", _ => ""
    };
    public bool TryGetHeld(byte actor, out CombatWeaponId weapon)
    {
        var held = Inventory.HeldWeapon(actor);
        if (held.IsEmpty) { weapon = CombatWeaponId.Hand; return true; }
        for (var i = 1; i <= (int)CombatWeaponId.Shotgun; i++)
            if (held.ItemId == WeaponItemId((CombatWeaponId)i)) { weapon = (CombatWeaponId)i; return true; }
        weapon = CombatWeaponId.Hand; return false;
    }
    public bool CanAttack(byte actor) => Inventory.CanAttack(actor) && TryGetHeld(actor, out _);
    public uint EquipmentGeneration(byte actor) => Inventory.EquipmentGeneration(actor);
    public bool TryGetAmmo(byte actor, CombatWeaponId weapon, out int magazine, out int reserve)
    {
        var held = Inventory.HeldWeapon(actor);
        magazine = held.MagazineKnown ? held.Magazine : 0; reserve = held.Ammo;
        return !held.IsEmpty && held.ItemId == WeaponItemId(weapon);
    }
    public void WriteAmmo(byte actor, CombatWeaponId weapon, int magazine, int reserve) =>
        Inventory.TrySetWeaponAmmo(actor, WeaponItemId(weapon), magazine, reserve);
    public void AttackStarted(byte actor)
    {
        Inventory.CancelForAction(actor, ItemCancelAction.Attack);
        foreach (var key in _effects.Keys.Where(k => k.Actor == actor && k.Effect == "Invisible").ToArray()) _effects.Remove(key);
    }
    public int MitigateDamage(byte actor, int rawDamage) => Inventory.AbsorbWeaponDamage(actor, rawDamage);
    public bool IsInvulnerable(byte actor, uint tick) => _invulnerable.TryGetValue(actor, out var until) && until > _now;
    public void GrantInvulnerability(byte actor, uint tick, double seconds) => _invulnerable[actor] = _now + seconds;
    public void ApplyStun(byte actor, uint tick, double seconds)
    {
        if (_effects.Any(e => e.Key.Actor == actor && e.Key.Effect == "Immune" && e.Value.End > _now)) return;
        _effects[(actor, 0, "Stun")] = (_now + seconds, 0);
        Inventory.CancelForAction(actor, ItemCancelAction.Stun);
    }
    public bool TryConsumeLifeSave(byte actor) => Inventory.TryConsumeLifePassive(actor, _combatTick == 0 ? 1 : _combatTick);
    public void SetCombatTick(uint tick) => _combatTick = tick;
    public bool IsBreakable(AABB obstacle) => TryFindDestructible(obstacle, out var id) && !Destructibles.IsBroken(id);
    public void ApplyDamage(AABB obstacle, int damage, byte attackerNetId)
    {
        if (!TryFindDestructible(obstacle, out var id)) return;
        if (++_confirmedWorldAction == 0) _confirmedWorldAction = 1;
        Destructibles.DamageConfirmed(id, damage, attackerNetId, _confirmedWorldAction);
    }
    private bool TryFindDestructible(AABB obstacle, out ushort id)
    {
        foreach (var definition in Layout.Objects)
        {
            if (!definition.IsDestructible) continue;
            var b = definition.Bounds;
            if (b.MinX == obstacle.MinX && b.MaxX == obstacle.MaxX && b.MinY == obstacle.MinY && b.MaxY == obstacle.MaxY)
            { id = definition.Id; return true; }
        }
        id = 0; return false;
    }
}
