#nullable disable
using System;
using LiteNetLib.Utils;
namespace JungleBackend.Protocol.Gameplay
{
    public enum WorldActionKind:byte { Resync=0,Pickup=1,Drop=2,Select=3,Use=4,CancelUse=5 }
    public static class WorldWire
    {
        public const byte Version=1;public const int PageSize=24,MaxObjects=4096;
        public static void Header(NetDataWriter w,Guid id){w.Put(Version);MatchWire.WriteGuid(w,id);}
        public static Guid Header(NetDataReader r){if(r.GetByte()!=Version)throw new InvalidOperationException("World protocol version mismatch.");return MatchWire.ReadGuid(r);}
        public static bool Finite(float n){return !float.IsNaN(n)&&!float.IsInfinity(n);}
        public static void Check(bool valid){if(!valid)throw new InvalidOperationException("Invalid world packet.");}
        public static void Page(ushort index,ushort pages,ushort total,int count)
        {
            var expectedPages=Math.Max(1,(total+PageSize-1)/PageSize);
            var expectedCount=Math.Min(PageSize,Math.Max(0,total-index*PageSize));
            Check(total<=MaxObjects&&pages==expectedPages&&index<pages&&count==expectedCount);
        }
    }
    public struct WorldActionRequestPacket:INetSerializable
    {
        public Guid MatchId;public uint RequestId;public WorldActionKind Action;public ushort TargetId;public byte Slot;
        public void Serialize(NetDataWriter w){WorldWire.Header(w,MatchId);w.Put(RequestId);w.Put((byte)Action);w.Put(TargetId);w.Put(Slot);}
        public void Deserialize(NetDataReader r){MatchId=WorldWire.Header(r);RequestId=r.GetUInt();Action=(WorldActionKind)r.GetByte();TargetId=r.GetUShort();Slot=r.GetByte();WorldWire.Check(RequestId!=0&&Action<=WorldActionKind.CancelUse&&(Slot<8||Slot==255));}
    }
    public struct WorldManifestPacket:INetSerializable
    {
        public Guid MatchId;public uint Seed;public string Version,Hash;public ushort Size;public uint Revision;
        public void Serialize(NetDataWriter w){WorldWire.Header(w,MatchId);w.Put(Seed);w.Put(Version??"");w.Put(Hash??"");w.Put(Size);w.Put(Revision);}
        public void Deserialize(NetDataReader r){MatchId=WorldWire.Header(r);Seed=r.GetUInt();Version=MatchWire.ReadString(r,48);Hash=MatchWire.ReadString(r,64);Size=r.GetUShort();Revision=r.GetUInt();WorldWire.Check(MatchId!=Guid.Empty&&Size>=30&&Size<=128&&Version.Length>0&&Hash.Length==64);foreach(var c in Hash)WorldWire.Check(c>='0'&&c<='9'||c>='a'&&c<='f');}
    }
    public struct WorldObjectWire
    {
        public ushort Id,PairId,Hp;public byte Kind,Flags;public float X,Y,Width,Height;public string ItemId;public ushort Count;
        public void Write(NetDataWriter w){w.Put(Id);w.Put(Kind);w.Put(Flags);w.Put(PairId);w.Put(Hp);w.Put(X);w.Put(Y);w.Put(Width);w.Put(Height);w.Put(ItemId??"");w.Put(Count);}
        public static WorldObjectWire Read(NetDataReader r){var o=new WorldObjectWire{Id=r.GetUShort(),Kind=r.GetByte(),Flags=r.GetByte(),PairId=r.GetUShort(),Hp=r.GetUShort(),X=r.GetFloat(),Y=r.GetFloat(),Width=r.GetFloat(),Height=r.GetFloat(),ItemId=MatchWire.ReadString(r,48),Count=r.GetUShort()};WorldWire.Check(o.Id!=0&&((o.Kind>=1&&o.Kind<=13)||o.Kind==100)&&WorldWire.Finite(o.X)&&WorldWire.Finite(o.Y)&&WorldWire.Finite(o.Width)&&WorldWire.Finite(o.Height)&&o.Width>0&&o.Height>0&&o.Flags<=3);return o;}
    }
    public struct WorldObjectStatePacket:INetSerializable
    {
        public Guid MatchId;public uint Revision,Tick;public ushort PageIndex,PageCount,TotalObjects;public WorldObjectWire[] Objects;
        public void Serialize(NetDataWriter w){var a=Objects??Array.Empty<WorldObjectWire>();WorldWire.Page(PageIndex,PageCount,TotalObjects,a.Length);WorldWire.Header(w,MatchId);w.Put(Revision);w.Put(Tick);w.Put(PageIndex);w.Put(PageCount);w.Put(TotalObjects);w.Put((byte)a.Length);foreach(var o in a)o.Write(w);}
        public void Deserialize(NetDataReader r){MatchId=WorldWire.Header(r);Revision=r.GetUInt();Tick=r.GetUInt();PageIndex=r.GetUShort();PageCount=r.GetUShort();TotalObjects=r.GetUShort();var n=r.GetByte();WorldWire.Page(PageIndex,PageCount,TotalObjects,n);Objects=new WorldObjectWire[n];for(var i=0;i<n;i++)Objects[i]=WorldObjectWire.Read(r);}
    }
    public struct InventorySlotWire
    {
        public string ItemId;public ushort Count,Ammo;public float Durability;
        public void Write(NetDataWriter w){w.Put(ItemId??"");w.Put(Count);w.Put(Ammo);w.Put(Durability);}
        public static InventorySlotWire Read(NetDataReader r){var s=new InventorySlotWire{ItemId=MatchWire.ReadString(r,48),Count=r.GetUShort(),Ammo=r.GetUShort(),Durability=r.GetFloat()};WorldWire.Check(WorldWire.Finite(s.Durability)&&s.Durability>=0);return s;}
    }
    public struct InventoryStatePacket:INetSerializable
    {
        public Guid MatchId;public byte Actor,ActiveWeapon,UseSlot;public uint Revision;public bool IsUsing;public string UseItemId;public float UseDuration,UseRemaining;public InventorySlotWire[] Slots;public InventorySlotWire Helmet,Armor;
        public void Serialize(NetDataWriter w){WorldWire.Check(Slots!=null&&Slots.Length==8);WorldWire.Header(w,MatchId);w.Put(Actor);w.Put(Revision);w.Put(ActiveWeapon);w.Put(IsUsing);w.Put(UseSlot);w.Put(UseItemId??"");w.Put(UseDuration);w.Put(UseRemaining);foreach(var s in Slots)s.Write(w);Helmet.Write(w);Armor.Write(w);}
        public void Deserialize(NetDataReader r){MatchId=WorldWire.Header(r);Actor=r.GetByte();Revision=r.GetUInt();ActiveWeapon=r.GetByte();IsUsing=r.GetBool();UseSlot=r.GetByte();UseItemId=MatchWire.ReadString(r,48);UseDuration=r.GetFloat();UseRemaining=r.GetFloat();WorldWire.Check(Actor<10&&(ActiveWeapon<4||ActiveWeapon==255)&&(UseSlot<4||UseSlot==255)&&WorldWire.Finite(UseDuration)&&WorldWire.Finite(UseRemaining)&&UseDuration>=0&&UseRemaining>=0&&UseRemaining<=UseDuration+.01f&&(!IsUsing||(UseSlot<4&&UseItemId.Length>0)));Slots=new InventorySlotWire[8];for(var i=0;i<8;i++)Slots[i]=InventorySlotWire.Read(r);Helmet=InventorySlotWire.Read(r);Armor=InventorySlotWire.Read(r);}
    }
    public struct WorldActionResultPacket : INetSerializable
    {
        public Guid MatchId;public uint RequestId;public bool Accepted;public string Reason;
        public void Serialize(NetDataWriter w){WorldWire.Header(w,MatchId);w.Put(RequestId);w.Put(Accepted);w.Put(Reason??"");}
        public void Deserialize(NetDataReader r){MatchId=WorldWire.Header(r);RequestId=r.GetUInt();Accepted=r.GetBool();Reason=MatchWire.ReadString(r,48);WorldWire.Check(RequestId!=0);}
    }
}
