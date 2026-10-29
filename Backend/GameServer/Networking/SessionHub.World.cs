using GameServer.Simulation;
using JungleBackend.Protocol.Gameplay;
using LiteNetLib.Utils;
namespace GameServer.Networking;

public sealed partial class SessionHub
{
    private void SetupWorldSubscriptions()
    {
        _frameValidator.Subscribe<WorldActionRequestPacket>(_=>{});
        _gameplayRegistry.Subscribe<WorldActionRequestPacket,Session>((request,session)=>
        {
            if(session.PlayerId==null||request.MatchId!=session.Room.Match.MatchId)return;
            // Bound resync/action rate independently of transport retries. No public confirmed-damage request exists.
            if(session.LastWorldRequestTick==session.Room.Match.CurrentTick)return;
            session.LastWorldRequestTick=session.Room.Match.CurrentTick;
            if(request.Action==WorldActionKind.Resync)
            {
                if(session.WorldEnabled&&session.Room.Match.CurrentTick-session.LastWorldResyncTick<30)return;
                if(!session.Room.Match.ProcessWorldRequest(session.PlayerId,request))
                {
                    var unavailable=new WorldActionResultPacket{MatchId=request.MatchId,RequestId=request.RequestId,Accepted=false,Reason="WorldDisabled"};
                    SendWorldPacket(session,ref unavailable);return;
                }
                session.WorldEnabled=true;
                session.LastWorldResyncTick=session.Room.Match.CurrentTick;
            }
            else if(!session.WorldEnabled)return;
            else
            {
                var accepted=session.Room.Match.ProcessWorldRequest(session.PlayerId,request);
                var result=new WorldActionResultPacket{MatchId=request.MatchId,RequestId=request.RequestId,Accepted=accepted,Reason=accepted?"Ok":session.Room.Match.World?.LastResult.ToString()??"Rejected"};
                SendWorldPacket(session,ref result);
            }
            SendWorldState(session,request.Action==WorldActionKind.Resync);
        });
    }
    private void TickWorldStreams()
    {
        foreach(var session in _sessions.Values)
            if(session.WorldEnabled&&session.PlayerId!=null&&session.Room.Match.World!=null&&session.Room.Match.CurrentTick-session.LastWorldInventoryTick>=15)
                SendWorldState(session,false);
    }
    private void SendWorldPacket<T>(Session session,ref T packet) where T:struct,INetSerializable
    {
        var writer=new NetDataWriter();_gameplayRegistry.WriteFrame(writer,ref packet);
        session.Peer.TrySend(writer.CopyData(),true,GameplayProtocol.GameplayChannel);
    }
    private void SendWorldState(Session session,bool force)
    {
        var match=session.Room.Match;var world=match.World;
        if(world==null||session.PlayerId==null||!match.Entities.TryGetBySession(session.PlayerId,out var actor)||actor==null)return;
        var inventory=world.SnapshotInventory(actor.NetId);
        SendWorldPacket(session,ref inventory);
        if(force||((session.LastWorldRevision!=world.Revision||session.LastWorldMatch!=match.MatchId)&&match.CurrentTick-session.LastWorldSyncTick>=15))
        {
            var manifest=new WorldManifestPacket{MatchId=match.MatchId,Seed=world.Layout.Seed,Version=world.Layout.Version,Hash=world.Layout.Hash,Size=(ushort)world.Layout.Size,Revision=world.Revision};
            SendWorldPacket(session,ref manifest);
            var all=world.SnapshotObjects();var pages=Math.Max(1,(all.Count+WorldWire.PageSize-1)/WorldWire.PageSize);
            for(var page=0;page<pages;page++)
            {
                var count=Math.Min(WorldWire.PageSize,all.Count-page*WorldWire.PageSize);
                var packet=new WorldObjectStatePacket{MatchId=match.MatchId,Revision=world.Revision,Tick=match.CurrentTick,PageIndex=(ushort)page,PageCount=(ushort)pages,TotalObjects=(ushort)all.Count,Objects=all.GetRange(page*WorldWire.PageSize,count).ToArray()};
                SendWorldPacket(session,ref packet);
            }
            session.LastWorldRevision=world.Revision;session.LastWorldMatch=match.MatchId;
            session.LastWorldSyncTick=match.CurrentTick;
        }
        session.LastWorldInventoryTick=match.CurrentTick;
    }
}
