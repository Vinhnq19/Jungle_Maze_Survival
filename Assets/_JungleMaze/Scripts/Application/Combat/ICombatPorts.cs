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

    /// <summary>
    /// Inbound combat results. The server sends accepted and rejected results to the whole room; the requester is
    /// identified by <c>NetId</c> matching its own id, and everyone else treats the packet as a remote action.
    /// </summary>
    public interface ICombatResultSource
    {
        event Action<AttackResultPacket> AttackResult;
        event Action<ReloadResultPacket> ReloadResult;
    }
}
