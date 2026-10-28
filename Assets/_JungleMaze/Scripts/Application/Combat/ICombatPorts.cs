using System;
using JungleBackend.Protocol.Gameplay;

namespace JungleMaze.App.Combat
{
    /// <summary>
    /// Outbound combat requests. The real implementation sends through the CLI-02 serializer; demos and tests use
    /// <see cref="Mocks.FakeCombatBackend"/>. The client never computes the result of these requests.
    /// </summary>
    public interface ICombatRequestSink
    {
        void SendAttack(AttackRequestPacket packet);
        void SendReload(ReloadRequestPacket packet);
    }

    /// <summary>Inbound per-request results, one for each request sent by this player.</summary>
    public interface ICombatResultSource
    {
        event Action<AttackResultPacket> AttackResult;
        event Action<ReloadResultPacket> ReloadResult;
    }

    /// <summary>
    /// Confirmed combat events that other players see. The wire protocol has no broadcast packet yet, so only the
    /// fake backend raises this today. It is kept outside the protocol on purpose.
    /// </summary>
    public interface ICombatEventSource
    {
        event Action<CombatBroadcast> Broadcast;
    }

    public enum CombatBroadcastKind : byte
    {
        Attack = 0,
        ReloadStarted = 1
    }

    /// <summary>A confirmed combat action of one player, visible to every peer after the server accepted it.</summary>
    public readonly struct CombatBroadcast
    {
        public CombatBroadcast(byte netId, CombatWeaponId weapon, CombatBroadcastKind kind, uint tick)
        {
            NetId = netId;
            Weapon = weapon;
            Kind = kind;
            Tick = tick;
        }

        public byte NetId { get; }
        public CombatWeaponId Weapon { get; }
        public CombatBroadcastKind Kind { get; }
        public uint Tick { get; }
    }
}
