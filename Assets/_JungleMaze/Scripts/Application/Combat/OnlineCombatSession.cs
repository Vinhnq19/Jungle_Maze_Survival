using System;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Hud.State;
using JungleMaze.App.Match;
using JungleMaze.App.Ports;
using JungleMaze.App.World;
using JungleMaze.Core.DI;
using JungleMaze.Core.Ticking;

namespace JungleMaze.App.Combat
{
    /// <summary>Connects the existing weapon controller to real packets and server-owned inventory.</summary>
    public sealed class OnlineCombatSession : IInitializable, ITickable, IDisposable, ICombatRequestSink
    {
        private readonly MatchSession _match;
        private readonly WorldClientSession _world;
        private readonly IBalanceConfigProvider _balance;
        private readonly IPacketSender _sender;
        private readonly IPacketReceiver _receiver;
        private readonly IHudStateWriter _hud;
        private Guid _matchId;
        private byte _actor=255;
        private uint _inventoryRevision;
        private bool _hasInventory;
        private int _activeSlot=-2;
        private uint _equipmentGeneration;
        private uint _cancelUseRevision;
        private bool _cancelUseSent;
        private bool _heldSupported=true;

        public OnlineCombatSession(MatchSession match, WorldClientSession world, IBalanceConfigProvider balance,
            IPacketSender sender, IPacketReceiver receiver, IHudStateWriter hud)
        { _match=match; _world=world; _balance=balance; _sender=sender; _receiver=receiver; _hud=hud; }

        public WeaponController Controller { get; private set; }
        public bool CanAct => Controller!=null && _heldSupported && _match.Connected && _match.Phase==MatchPhase.InGame &&
            _match.OwnHp>0 && _match.CanSendGameplayInput && (!_world.Required || _world.IsReady);
        public event Action<CombatEvent> Changed;

        public void Initialize()
        { _receiver.Subscribe<AttackResultPacket>(Attack); _receiver.Subscribe<ReloadResultPacket>(Reload); _world.Changed+=ApplyInventory; }
        public void Dispose()
        { _receiver.Unsubscribe<AttackResultPacket>(Attack); _receiver.Unsubscribe<ReloadResultPacket>(Reload); _world.Changed-=ApplyInventory; Clear(); }

        public void Tick(float dt)
        {
            if (!_match.Connected || _match.Phase!=MatchPhase.InGame || _match.OwnNetId==255)
            { Clear(); return; }
            if (Controller==null || _matchId!=_match.MatchId || _actor!=_match.OwnNetId)
            {
                Clear(); _matchId=_match.MatchId; _actor=_match.OwnNetId;
                Controller=new WeaponController(_balance,this,_hud,_actor); Controller.Changed+=Forward;
                ApplyInventory();
            }
            Controller.Tick(dt);
        }
        private void ApplyInventory()
        {
            var state=_world.Inventory;
            if (Controller==null || state==null || state.Actor!=_actor || !_world.IsReady ||
                (_hasInventory && state.Revision==_inventoryRevision)) return;
            _inventoryRevision=state.Revision; _hasInventory=true;
            var item=state.ActiveWeapon>=0 && state.ActiveWeapon<4?state.Weapons[state.ActiveWeapon]:default;
            Controller.ApplyEquipment(item.IsEmpty?"HAND":item.ItemId,item.MagazineKnown?item.Magazine:-1,item.Ammo,
                _activeSlot!=state.ActiveWeapon || _equipmentGeneration!=_world.EquipmentGeneration);
            _heldSupported=item.IsEmpty || CombatWeaponIds.TryFromJsonId(item.ItemId,out _);
            if(!_heldSupported){_hud.SetHeldWeapon(item.ItemId);_hud.SetAmmo(0,item.Ammo);}
            _activeSlot=state.ActiveWeapon;
            _equipmentGeneration=_world.EquipmentGeneration;
        }
        public bool TryAttack(float aimDegrees)
        {
            if (!CanAct) return false;
            var inventory=_world.Inventory;
            if (inventory!=null && inventory.IsUsing && (!_cancelUseSent || _cancelUseRevision!=inventory.Revision))
            {
                if (!_world.Send(WorldActionKind.CancelUse)) return false;
                _cancelUseSent=true; _cancelUseRevision=inventory.Revision;
            }
            return Controller.TryAttack(aimDegrees);
        }
        public bool TryReload() => CanAct && Controller.TryReload();
        public void SendAttack(AttackRequestPacket packet)
        { if(CanAct) _sender.Send(ref packet,PacketDelivery.ReliableOrdered); }
        public void SendReload(ReloadRequestPacket packet)
        { if(CanAct) _sender.Send(ref packet,PacketDelivery.ReliableOrdered); }
        private void Attack(AttackResultPacket packet) { if (_match.Connected) Controller?.OnAttackResult(packet); }
        private void Reload(ReloadResultPacket packet) { if (_match.Connected) Controller?.OnReloadResult(packet); }
        private void Forward(CombatEvent value) => Changed?.Invoke(value);
        private void Clear()
        {
            if(Controller!=null) Controller.Changed-=Forward;
            Controller=null; _matchId=Guid.Empty; _actor=255; _hasInventory=false; _activeSlot=-2; _equipmentGeneration=0; _cancelUseSent=false; _heldSupported=true;
        }
    }
}
