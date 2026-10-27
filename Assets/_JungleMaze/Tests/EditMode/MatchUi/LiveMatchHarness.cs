using System;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Match;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Network;
using JungleMaze.Domain.Player;
using JungleMaze.Tests.App;

namespace JungleMaze.Tests.MatchUi
{
    /// <summary>A real <see cref="MatchSession"/> over the fake network, driven by pushed packets (no server).</summary>
    internal sealed class LiveMatchHarness : IDisposable
    {
        public readonly EventBus Bus = new EventBus();
        public readonly FakeNetwork Network;
        public readonly PlayerRegistry Players = new PlayerRegistry();
        public readonly MatchSession Match;
        public readonly Guid MatchId = Guid.NewGuid();
        public uint Tick = 100;

        public LiveMatchHarness()
        {
            Network = new FakeNetwork(Bus);
            Match = new MatchSession(Network, Network, Network, Bus, Players);
            Match.Initialize();
            Network.SetState(NetworkState.Joined, DisconnectReason.None);
        }

        /// <summary>InGame phase, own NetId, a roster of <paramref name="count"/> players and a first snapshot.</summary>
        public void Start(byte ownNetId, int count)
        {
            Network.Push(new MatchPhasePacket { MatchId = MatchId, Phase = MatchPhase.InGame, ServerTick = Tick, MaxPlayers = 10, MinPlayers = 1, PlayerCount = (byte)count });
            Network.Push(new ResumeTokenPacket { MatchId = MatchId, NetId = ownNetId, ResumeToken = Guid.NewGuid() });
            Roster(count);
            var ids = new byte[count];
            for (var i = 0; i < count; i++) ids[i] = (byte)i;
            Snapshot(ids);
        }

        public void Roster(int count, Func<byte, byte> rank = null, Func<byte, uint> survivalTicks = null)
        {
            var players = new MatchParticipantState[count];
            for (byte i = 0; i < count; i++)
                players[i] = new MatchParticipantState { NetId = i, DisplayName = "P" + i, Rank = rank?.Invoke(i) ?? 0, SurvivalTicks = survivalTicks?.Invoke(i) ?? 0 };
            Network.Push(new MatchRosterPacket { MatchId = MatchId, Players = players });
        }

        /// <summary>Snapshot with the given players alive (HP 100) and the rest of <paramref name="dead"/> at HP 0.</summary>
        public void Snapshot(byte[] alive, byte[] dead = null)
        {
            Tick += 3;
            var total = alive.Length + (dead?.Length ?? 0);
            var packet = new WorldSnapshotPacket { MatchId = MatchId, ServerTick = Tick, PlayerCount = (byte)total };
            var n = 0;
            foreach (var id in alive)
                packet.SetPlayer(n++, new PlayerStatePacket { Tick = Tick, NetId = id, X = 5 + id * 3, Y = 10, Hp = 100 });
            if (dead != null)
                foreach (var id in dead)
                    packet.SetPlayer(n++, new PlayerStatePacket { Tick = Tick, NetId = id, X = 5 + id * 3, Y = 10, Hp = 0, Flags = PlayerStateFlags.Dead });
            Network.Push(packet);
        }

        public void Entity(byte netId, MatchEntityEvent kind) =>
            Network.Push(new MatchEntityEventPacket { MatchId = MatchId, NetId = netId, Kind = kind });

        public void Dispose() => Match.Dispose();
    }
}
