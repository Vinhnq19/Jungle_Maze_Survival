#nullable enable
using System;
using LiteNetLib.Utils;

namespace JungleBackend.Protocol.Gameplay
{
    public static class MatchProtocol { public const ushort Version = 1; public const int MaxPlayers = 10; }
    public enum MatchPhase : byte { Waiting, Lobby, Loading, InGame, Ending, Cleanup }
    public enum MatchEndReason : byte { None, LastAlive, Tie, Timeout, Cancelled }
    public enum MatchEntityEvent : byte { Spawn, Despawn, Eliminated }
    public enum EliminationCauseWire : byte { Unknown = 0, Player = 1, Zone = 2, LeftMatch = 3, Disconnected = 4, Environment = 5 }

    // Guid's byte-array helpers allocate. The wire retains Guid byte order without allocating in snapshot/input loops.
    internal static class MatchWire
    {
        public static void WriteGuid(NetDataWriter writer, Guid value)
        {
            Span<byte> bytes = stackalloc byte[16];
            value.TryWriteBytes(bytes);
            for (var i = 0; i < 16; i++) writer.Put(bytes[i]);
        }
        public static Guid ReadGuid(NetDataReader reader)
        {
            Span<byte> bytes = stackalloc byte[16];
            for (var i = 0; i < 16; i++) bytes[i] = reader.GetByte();
            return new Guid(bytes);
        }
        public static string ReadString(NetDataReader reader, int maximum)
        {
            var value = reader.GetString();
            if (value.Length > maximum) throw new InvalidOperationException("Oversized match string.");
            return value;
        }
        public static void Count(int count)
        { if (count < 0 || count > MatchProtocol.MaxPlayers) throw new InvalidOperationException("Invalid player count."); }
    }

    public struct ClientHelloPacket : INetSerializable
    {
        public ushort ProtocolVersion; public bool Ready;
        public void Serialize(NetDataWriter w) { w.Put(ProtocolVersion); w.Put(Ready); }
        public void Deserialize(NetDataReader r) { ProtocolVersion=r.GetUShort(); Ready=r.GetBool(); }
    }
    public struct MatchPhasePacket : INetSerializable
    {
        public Guid MatchId; public MatchPhase Phase;
        public string ConfigVersion, ConfigHash;
        public uint ServerTick, PhaseStartTick, PhaseEndTick;
        public byte PlayerCount, MinPlayers, MaxPlayers;
        public void Serialize(NetDataWriter w)
        { MatchWire.WriteGuid(w,MatchId);w.Put((byte)Phase);w.Put(ServerTick);w.Put(PhaseStartTick);w.Put(PhaseEndTick);w.Put(PlayerCount);w.Put(MinPlayers);w.Put(MaxPlayers);w.Put(ConfigVersion??"");w.Put(ConfigHash??""); }
        public void Deserialize(NetDataReader r)
        {
            MatchId=MatchWire.ReadGuid(r);Phase=(MatchPhase)r.GetByte();ServerTick=r.GetUInt();PhaseStartTick=r.GetUInt();PhaseEndTick=r.GetUInt();
            PlayerCount=r.GetByte();MinPlayers=r.GetByte();MaxPlayers=r.GetByte();ConfigVersion=MatchWire.ReadString(r,32);ConfigHash=MatchWire.ReadString(r,64);
            if(Phase>MatchPhase.Cleanup || PlayerCount>10 || MaxPlayers>10 || MinPlayers<1) throw new InvalidOperationException("Invalid phase.");
        }
    }
    public struct LoadAckPacket : INetSerializable
    {
        public Guid MatchId; public bool Ready;
        public void Serialize(NetDataWriter w) { MatchWire.WriteGuid(w,MatchId);w.Put(Ready); }
        public void Deserialize(NetDataReader r) { MatchId=MatchWire.ReadGuid(r);Ready=r.GetBool(); }
    }
    public struct MatchInputPacket : INetSerializable
    {
        public Guid MatchId; public PlayerInputPacket Input;
        public void Serialize(NetDataWriter w) { MatchWire.WriteGuid(w,MatchId);Input.Serialize(w); }
        public void Deserialize(NetDataReader r) { MatchId=MatchWire.ReadGuid(r);Input.Deserialize(r);if(!Input.IsValid)throw new InvalidOperationException("Invalid input."); }
    }
    public struct ZoneStatePacket : INetSerializable
    {
        public Guid MatchId; public uint Tick; public byte PhaseIndex; public bool InWarning;
        public float CurrentCenterX,CurrentCenterY,CurrentRadius,TargetCenterX,TargetCenterY,TargetRadius;
        public uint ShrinkStartTick,ShrinkEndTick;
        public void Serialize(NetDataWriter w)
        {
            MatchWire.WriteGuid(w,MatchId);w.Put(Tick);w.Put(PhaseIndex);w.Put(InWarning);
            w.Put(CurrentCenterX);w.Put(CurrentCenterY);w.Put(CurrentRadius);w.Put(TargetCenterX);w.Put(TargetCenterY);w.Put(TargetRadius);
            w.Put(ShrinkStartTick);w.Put(ShrinkEndTick);
        }
        public void Deserialize(NetDataReader r)
        {
            MatchId=MatchWire.ReadGuid(r);Tick=r.GetUInt();PhaseIndex=r.GetByte();InWarning=r.GetBool();
            CurrentCenterX=r.GetFloat();CurrentCenterY=r.GetFloat();CurrentRadius=r.GetFloat();TargetCenterX=r.GetFloat();TargetCenterY=r.GetFloat();TargetRadius=r.GetFloat();
            ShrinkStartTick=r.GetUInt();ShrinkEndTick=r.GetUInt();
            if(!Finite(CurrentCenterX)||!Finite(CurrentCenterY)||!Finite(CurrentRadius)||!Finite(TargetCenterX)||!Finite(TargetCenterY)||!Finite(TargetRadius)||CurrentRadius<0||TargetRadius<0)
                throw new InvalidOperationException("Invalid zone.");
        }
        private static bool Finite(float v) => !float.IsNaN(v)&&!float.IsInfinity(v);
    }

    /// <summary>Ten inline value slots keep deserialization allocation-free and isolate simultaneous clients/rooms.</summary>
    public struct WorldSnapshotPacket : INetSerializable
    {
        public const int MaxPlayers=10;
        public Guid MatchId; public uint ServerTick; public byte PlayerCount;
        private PlayerStatePacket _p0,_p1,_p2,_p3,_p4,_p5,_p6,_p7,_p8,_p9;
        // Compatibility convenience for setup/tests only. Hot-path callers use GetPlayer/SetPlayer.
        public PlayerStatePacket[]? Players
        {
            get { var result=new PlayerStatePacket[PlayerCount];for(var i=0;i<PlayerCount;i++)result[i]=GetPlayer(i);return result; }
            set { MatchWire.Count(value?.Length??0);PlayerCount=(byte)(value?.Length??0);for(var i=0;i<PlayerCount;i++)SetPlayer(i,value![i]); }
        }
        public PlayerStatePacket GetPlayer(int i)
        { switch(i){case 0:return _p0;case 1:return _p1;case 2:return _p2;case 3:return _p3;case 4:return _p4;case 5:return _p5;case 6:return _p6;case 7:return _p7;case 8:return _p8;case 9:return _p9;default:throw new ArgumentOutOfRangeException(nameof(i));} }
        public void SetPlayer(int i,PlayerStatePacket p)
        { switch(i){case 0:_p0=p;break;case 1:_p1=p;break;case 2:_p2=p;break;case 3:_p3=p;break;case 4:_p4=p;break;case 5:_p5=p;break;case 6:_p6=p;break;case 7:_p7=p;break;case 8:_p8=p;break;case 9:_p9=p;break;default:throw new ArgumentOutOfRangeException(nameof(i));} }
        public void Serialize(NetDataWriter w)
        { MatchWire.Count(PlayerCount);MatchWire.WriteGuid(w,MatchId);w.Put(ServerTick);w.Put(PlayerCount);for(var i=0;i<PlayerCount;i++){var p=GetPlayer(i);p.Serialize(w);} }
        public void Deserialize(NetDataReader r)
        {
            MatchId=MatchWire.ReadGuid(r);ServerTick=r.GetUInt();PlayerCount=r.GetByte();MatchWire.Count(PlayerCount);
            if(r.AvailableBytes<PlayerCount*PlayerStatePacket.PayloadBytes)throw new InvalidOperationException("Truncated snapshot.");
            ushort seen=0;
            for(var i=0;i<PlayerCount;i++)
            { var p=new PlayerStatePacket();p.Deserialize(r);if(!p.IsValid||p.NetId>=10||(seen&(1<<p.NetId))!=0)throw new InvalidOperationException("Invalid snapshot player.");seen|=(ushort)(1<<p.NetId);SetPlayer(i,p); }
        }
    }
    public struct SnapshotAckPacket : INetSerializable
    {
        public Guid MatchId; public uint AckTick;
        public void Serialize(NetDataWriter w){MatchWire.WriteGuid(w,MatchId);w.Put(AckTick);}
        public void Deserialize(NetDataReader r){MatchId=MatchWire.ReadGuid(r);AckTick=r.GetUInt();}
    }
    public struct ResumeTokenPacket : INetSerializable
    {
        public Guid MatchId,ResumeToken;public byte NetId;
        public void Serialize(NetDataWriter w){MatchWire.WriteGuid(w,MatchId);w.Put(NetId);MatchWire.WriteGuid(w,ResumeToken);}
        public void Deserialize(NetDataReader r){MatchId=MatchWire.ReadGuid(r);NetId=r.GetByte();ResumeToken=MatchWire.ReadGuid(r);if(NetId>=10)throw new InvalidOperationException("Invalid slot.");}
    }
    public struct PlayerLeavePacket : INetSerializable
    {
        public Guid MatchId;
        public void Serialize(NetDataWriter w){MatchWire.WriteGuid(w,MatchId);}
        public void Deserialize(NetDataReader r){MatchId=MatchWire.ReadGuid(r);}
    }
    public struct ReplayAckPacket : INetSerializable
    {
        public Guid MatchId;
        public void Serialize(NetDataWriter w){MatchWire.WriteGuid(w,MatchId);}
        public void Deserialize(NetDataReader r){MatchId=MatchWire.ReadGuid(r);}
    }
    public struct AttachIdentityPacket : INetSerializable
    {
        public string Ticket;
        public void Serialize(NetDataWriter w){if(string.IsNullOrEmpty(Ticket)||Ticket.Length>64)throw new InvalidOperationException("Invalid ticket.");w.Put(Ticket);}
        public void Deserialize(NetDataReader r){Ticket=MatchWire.ReadString(r,64);if(Ticket.Length==0)throw new InvalidOperationException("Invalid ticket.");}
    }
    public struct MatchParticipantState : INetSerializable
    {
        public byte NetId,Rank;public ushort Kills;public uint SurvivalTicks;
        public string PlayerSessionId,DisplayName;
        public void Serialize(NetDataWriter w)
        { w.Put(NetId);w.Put(PlayerSessionId??"");w.Put(DisplayName??"");w.Put(Rank);w.Put(Kills);w.Put(SurvivalTicks); }
        public void Deserialize(NetDataReader r)
        { NetId=r.GetByte();PlayerSessionId=MatchWire.ReadString(r,64);DisplayName=MatchWire.ReadString(r,32);Rank=r.GetByte();Kills=r.GetUShort();SurvivalTicks=r.GetUInt();if(NetId>=10||Rank>10)throw new InvalidOperationException("Invalid participant."); }
    }
    public struct MatchRosterPacket : INetSerializable
    {
        public Guid MatchId;public MatchParticipantState[]? Players;
        public void Serialize(NetDataWriter w)
        { MatchWire.Count(Players?.Length??0);MatchWire.WriteGuid(w,MatchId);w.Put((byte)(Players?.Length??0));if(Players!=null)for(var i=0;i<Players.Length;i++)Players[i].Serialize(w); }
        public void Deserialize(NetDataReader r)
        { MatchId=MatchWire.ReadGuid(r);var n=r.GetByte();MatchWire.Count(n);Players=new MatchParticipantState[n];for(var i=0;i<n;i++)Players[i].Deserialize(r); }
    }
    public struct MatchEntityEventPacket : INetSerializable
    {
        public Guid MatchId;public byte NetId;public MatchEntityEvent Kind;
        public void Serialize(NetDataWriter w){MatchWire.WriteGuid(w,MatchId);w.Put(NetId);w.Put((byte)Kind);}
        public void Deserialize(NetDataReader r){MatchId=MatchWire.ReadGuid(r);NetId=r.GetByte();Kind=(MatchEntityEvent)r.GetByte();if(NetId>=10||Kind>MatchEntityEvent.Eliminated)throw new InvalidOperationException("Invalid entity event.");}
    }
    public struct MatchResultPacket : INetSerializable
    {
        public Guid MatchId; public MatchEndReason Reason; public byte WinnerNetId,ParticipantCount;
        public MatchParticipantState[]? Participants;
        public void Serialize(NetDataWriter w)
        {
            var n=Participants?.Length??0;MatchWire.Count(n);MatchWire.WriteGuid(w,MatchId);w.Put((byte)Reason);w.Put(WinnerNetId);w.Put((byte)n);
            if(Participants!=null)for(var i=0;i<n;i++)Participants[i].Serialize(w);
        }
        public void Deserialize(NetDataReader r)
        {
            MatchId=MatchWire.ReadGuid(r);Reason=(MatchEndReason)r.GetByte();WinnerNetId=r.GetByte();ParticipantCount=r.GetByte();MatchWire.Count(ParticipantCount);
            if(Reason>MatchEndReason.Cancelled)throw new InvalidOperationException("Invalid match result.");
            Participants=new MatchParticipantState[ParticipantCount];for(var i=0;i<ParticipantCount;i++)Participants[i].Deserialize(r);
        }
    }

    public struct MatchEliminationEventPacket : INetSerializable
    {
        public const byte CurrentVersion = 1;
        public byte Version;
        public Guid MatchId;
        public uint Tick;
        public byte VictimNetId;
        public byte KillerNetId;
        public EliminationCauseWire Cause;

        public void Serialize(NetDataWriter w)
        {
            w.Put(Version);
            MatchWire.WriteGuid(w, MatchId);
            w.Put(Tick);
            w.Put(VictimNetId);
            w.Put(KillerNetId);
            w.Put((byte)Cause);
        }

        public void Deserialize(NetDataReader r)
        {
            Version = r.GetByte();
            if (Version != CurrentVersion) throw new InvalidOperationException("Unsupported elimination schema version.");
            MatchId = MatchWire.ReadGuid(r);
            Tick = r.GetUInt();
            VictimNetId = r.GetByte();
            KillerNetId = r.GetByte();
            Cause = (EliminationCauseWire)r.GetByte();
            if (VictimNetId >= MatchProtocol.MaxPlayers || (KillerNetId >= MatchProtocol.MaxPlayers && KillerNetId != 255) || Cause > EliminationCauseWire.Environment)
                throw new InvalidOperationException("Invalid elimination event.");
        }
    }

    public struct KillcamRequestPacket : INetSerializable
    {
        public const byte CurrentVersion = 1;
        public byte Version;
        public Guid MatchId;
        public byte VictimNetId;

        public void Serialize(NetDataWriter w)
        {
            w.Put(Version);
            MatchWire.WriteGuid(w, MatchId);
            w.Put(VictimNetId);
        }

        public void Deserialize(NetDataReader r)
        {
            Version = r.GetByte();
            if (Version != CurrentVersion) throw new InvalidOperationException("Unsupported killcam request version.");
            MatchId = MatchWire.ReadGuid(r);
            VictimNetId = r.GetByte();
            if (VictimNetId >= MatchProtocol.MaxPlayers) throw new InvalidOperationException("Invalid victim netId.");
        }
    }

    public struct KillcamFramePayload
    {
        public float ZoneX;
        public float ZoneY;
        public float ZoneRadius;
        public float VictimX;
        public float VictimY;
        public byte VictimHp;
        public byte VictimFlags;
        public float KillerX;
        public float KillerY;
        public byte KillerHp;
        public byte KillerFlags;
        public bool KillerPresent;

        public void Serialize(NetDataWriter w)
        {
            w.Put(ZoneX);
            w.Put(ZoneY);
            w.Put(ZoneRadius);
            w.Put(VictimX);
            w.Put(VictimY);
            w.Put(VictimHp);
            w.Put(VictimFlags);
            w.Put(KillerX);
            w.Put(KillerY);
            w.Put(KillerHp);
            w.Put(KillerFlags);
            w.Put(KillerPresent);
        }

        public void Deserialize(NetDataReader r)
        {
            ZoneX = r.GetFloat();
            ZoneY = r.GetFloat();
            ZoneRadius = r.GetFloat();
            VictimX = r.GetFloat();
            VictimY = r.GetFloat();
            VictimHp = r.GetByte();
            VictimFlags = r.GetByte();
            KillerX = r.GetFloat();
            KillerY = r.GetFloat();
            KillerHp = r.GetByte();
            KillerFlags = r.GetByte();
            KillerPresent = r.GetBool();
            if (!Finite(ZoneX) || !Finite(ZoneY) || !Finite(ZoneRadius) || ZoneRadius < 0f ||
                !Finite(VictimX) || !Finite(VictimY) ||
                (KillerPresent && (!Finite(KillerX) || !Finite(KillerY))))
            {
                throw new InvalidOperationException("Invalid killcam frame.");
            }
        }

        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }

    public struct KillcamDataChunkPacket : INetSerializable
    {
        public const byte CurrentVersion = 1;
        public const int MaxFramesPerChunk = 75;
        public const int MaxTotalFrames = 150;
        public const int MaxChunks = 5;

        public byte Version;
        public Guid MatchId;
        public byte Status;
        public byte VictimNetId;
        public byte KillerNetId;
        public uint EndTick;
        public ushort TotalFrames;
        public byte ChunkIndex;
        public byte TotalChunks;
        public byte FrameCount;
        public KillcamFramePayload[]? Frames;

        public void ValidateHeader()
        {
            if (Version != CurrentVersion || VictimNetId >= MatchProtocol.MaxPlayers || Status > 9)
                throw new InvalidOperationException("Invalid killcam header.");
            if (Status != 0)
            {
                if (TotalFrames != 0 || TotalChunks != 1 || ChunkIndex != 0 || FrameCount != 0)
                    throw new InvalidOperationException("Unavailable replay must have no frames.");
                return;
            }
            if (KillerNetId >= MatchProtocol.MaxPlayers || KillerNetId == VictimNetId || TotalFrames == 0 || TotalFrames > MaxTotalFrames)
                throw new InvalidOperationException("Invalid replay participants or window.");
            var chunks = (TotalFrames + MaxFramesPerChunk - 1) / MaxFramesPerChunk;
            var remaining = TotalFrames - ChunkIndex * MaxFramesPerChunk;
            if (TotalChunks != chunks || ChunkIndex >= chunks || FrameCount != Math.Min(MaxFramesPerChunk, remaining))
                throw new InvalidOperationException("Incomplete or overlapping killcam chunks.");
        }

        public void Serialize(NetDataWriter w)
        {
            var length = Frames?.Length ?? 0;
            if (length > MaxFramesPerChunk) throw new InvalidOperationException("Oversized killcam payload.");
            FrameCount = (byte)length;
            ValidateHeader();
            var count = FrameCount;
            w.Put(Version);
            MatchWire.WriteGuid(w, MatchId);
            w.Put(Status);
            w.Put(VictimNetId);
            w.Put(KillerNetId);
            w.Put(EndTick);
            w.Put(TotalFrames);
            w.Put(ChunkIndex);
            w.Put(TotalChunks);
            w.Put(count);
            if (Frames != null)
            {
                for (var i = 0; i < count; i++)
                {
                    Frames[i].Serialize(w);
                }
            }
        }

        public void Deserialize(NetDataReader r)
        {
            Version = r.GetByte();
            if (Version != CurrentVersion) throw new InvalidOperationException("Unsupported killcam chunk version.");
            MatchId = MatchWire.ReadGuid(r);
            Status = r.GetByte();
            VictimNetId = r.GetByte();
            KillerNetId = r.GetByte();
            EndTick = r.GetUInt();
            TotalFrames = r.GetUShort();
            ChunkIndex = r.GetByte();
            TotalChunks = r.GetByte();
            FrameCount = r.GetByte();

            if (VictimNetId >= MatchProtocol.MaxPlayers || (KillerNetId >= MatchProtocol.MaxPlayers && KillerNetId != 255))
                throw new InvalidOperationException("Invalid player in killcam chunk.");
            if (TotalFrames > MaxTotalFrames)
                throw new InvalidOperationException("Oversized total frame count.");
            if (TotalChunks > MaxChunks)
                throw new InvalidOperationException("Oversized chunk count.");
            if (TotalChunks > 0 && ChunkIndex >= TotalChunks)
                throw new InvalidOperationException("Invalid chunk index.");
            if (FrameCount > MaxFramesPerChunk)
                throw new InvalidOperationException("Oversized chunk frame count.");

            ValidateHeader();
            if (FrameCount > 0)
            {
                Frames = new KillcamFramePayload[FrameCount];
                for (var i = 0; i < FrameCount; i++)
                {
                    var f = new KillcamFramePayload();
                    f.Deserialize(r);
                    Frames[i] = f;
                }
            }
            else
            {
                Frames = Array.Empty<KillcamFramePayload>();
            }
        }
    }
}
