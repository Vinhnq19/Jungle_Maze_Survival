using System;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Combat;
using JungleMaze.App.Combat.Mocks;
using JungleMaze.App.Hud.State;
using JungleMaze.App.Ports;
using JungleMaze.World.Inventory;

namespace JungleMaze.App.World
{
    /// <summary>Explicit offline preview. Real matches continue to use OnlineCombatSession.</summary>
    public sealed class WorldDemoCombatSession : IDisposable
    {
        private readonly InventoryService _items;
        private readonly FakeCombatBackend _backend;
        private readonly FakeCombatPeer _peer;
        private uint _revision=uint.MaxValue, _equipment=uint.MaxValue;
        private float _fraction, _aim;
        private bool _supported;
        public WeaponController Controller { get; }
        public event Action<CombatWeaponId,float> Confirmed;

        public WorldDemoCombatSession(IBalanceConfigProvider balance, IHudStateWriter hud, InventoryService items)
        {
            _items=items;_backend=new FakeCombatBackend(balance);_peer=_backend.AddPeer(0);
            Controller=new WeaponController(balance,_peer,hud,0);
            _peer.AttackResult+=AttackResult;_peer.ReloadResult+=Controller.OnReloadResult;
            Synchronize();
        }
        public void Synchronize()
        {
            var state=_items.GetSnapshot(0);var generation=_items.EquipmentGeneration(0);
            if(state.Revision==_revision&&generation==_equipment)return;
            var held=_items.HeldWeapon(0);var id=held.IsEmpty?"HAND":held.ItemId;
            _supported=CombatWeaponIds.TryFromJsonId(id,out var weapon);
            if(!_supported)weapon=CombatWeaponId.Hand;
            _peer.SynchronizeEquipment(weapon,held.Magazine,held.Ammo,generation!=_equipment);
            Controller.ApplyEquipment(id,held.Magazine,held.Ammo,generation!=_equipment);
            _revision=state.Revision;_equipment=generation;
        }
        public bool TryAttack(float aim)
        {
            Synchronize();if(!_supported||!_items.CanAttack(0))return false;
            _aim=aim;if(!Controller.TryAttack(aim))return false;
            _backend.Flush();PersistAmmo();return true;
        }
        public bool TryReload()
        {
            Synchronize();if(!_supported||!_items.CanAttack(0)||!Controller.TryReload())return false;
            _backend.Flush();return true;
        }
        public void Tick(float seconds)
        {
            Synchronize();_fraction+=Math.Max(0,seconds);
            while(_fraction>=1f/30){_fraction-=1f/30;_backend.AdvanceTicks(1);}
            PersistAmmo();Controller.Tick(seconds);
        }
        private void PersistAmmo()
        {
            if(_supported&&Controller.SelectedSlot.IsRanged)
                _items.TrySetWeaponAmmo(0,CombatWeaponIds.ToJsonId(Controller.Selected),_peer.MagazineOf(Controller.Selected),_peer.ReserveOf(Controller.Selected));
        }
        private void AttackResult(AttackResultPacket packet)
        {
            Controller.OnAttackResult(packet);
            if(packet.Outcome!=CombatOutcome.Accepted)return;
            _items.CancelForAction(0,ItemCancelAction.Attack);Confirmed?.Invoke(packet.Weapon,_aim);
        }
        public void Dispose()
        {_peer.AttackResult-=AttackResult;_peer.ReloadResult-=Controller.OnReloadResult;}
    }
}
