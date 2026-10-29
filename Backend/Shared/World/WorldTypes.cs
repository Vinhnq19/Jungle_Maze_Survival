#nullable disable
using System;
using System.Collections.Generic;
namespace JungleMaze.World
{
    public struct WorldPoint { public float X, Y; public WorldPoint(float x,float y){X=x;Y=y;} public float DistanceSquared(WorldPoint p){var x=X-p.X;var y=Y-p.Y;return x*x+y*y;} }
    public struct WorldBounds
    {
        public float MinX,MinY,MaxX,MaxY;
        public WorldBounds(float x,float y,float width,float height){MinX=x-width/2;MaxX=x+width/2;MinY=y-height/2;MaxY=y+height/2;}
        public WorldPoint Center {get{return new WorldPoint((MinX+MaxX)/2,(MinY+MaxY)/2);}}
        public bool Contains(WorldPoint p,float margin=0){return p.X>=MinX-margin&&p.X<=MaxX+margin&&p.Y>=MinY-margin&&p.Y<=MaxY+margin;}
        public bool Overlaps(WorldBounds b,float gap=0){return MinX<b.MaxX+gap&&MaxX>b.MinX-gap&&MinY<b.MaxY+gap&&MaxY>b.MinY-gap;}
        public bool IntersectsCircle(WorldPoint p,float radius){var x=Math.Max(MinX,Math.Min(MaxX,p.X));var y=Math.Max(MinY,Math.Min(MaxY,p.Y));return (x-p.X)*(x-p.X)+(y-p.Y)*(y-p.Y)<radius*radius;}
    }
    public enum WorldObjectKind:byte { Pillar=1,Wall=2,Rock=3,Statue=4,Chest=5,Pot=6,Bush=7,Swamp=8,Lava=9,Poison=10,Portal=11,Door=12,Supply=13 }
    public sealed class WorldObjectDefinition
    {
        public ushort Id; public WorldObjectKind Kind; public WorldBounds Bounds; public ushort PairId;
        public bool IsDestructible {get{return Kind==WorldObjectKind.Chest||Kind==WorldObjectKind.Pot||Kind==WorldObjectKind.Supply;}}
        public bool IsHardCover {get{return Kind>=WorldObjectKind.Pillar&&Kind<=WorldObjectKind.Statue;}}
        public bool IsHazard {get{return Kind==WorldObjectKind.Swamp||Kind==WorldObjectKind.Lava||Kind==WorldObjectKind.Poison;}}
    }
    public sealed class MapLayoutConfig
    {
        public string Version="world-prototype-1"; public int Size=50,SpawnCount=10,ChestCount=60,PotCount=90,BushCount=45,PortalPairs=3,DoorCount=2,SwampCount=2,LavaCount=2,PoisonCount=4;
        public float SpawnMinDistance=6,HeroRadius=.4f,InitialZoneRadius=36;
        public void Validate(){if(string.IsNullOrWhiteSpace(Version)||Size<30||Size>128||SpawnCount!=10||ChestCount<0||PotCount<0||BushCount<0||PortalPairs<0||PortalPairs>16||DoorCount<0||SwampCount<0||LavaCount<0||PoisonCount<0||!WorldMath.Finite(SpawnMinDistance)||SpawnMinDistance<1||!WorldMath.Finite(HeroRadius)||HeroRadius<=0||!WorldMath.Finite(InitialZoneRadius)||InitialZoneRadius<=0)throw new ArgumentException("Invalid world map configuration.");if((long)ChestCount+PotCount+BushCount+PortalPairs*2+DoorCount+SwampCount+LavaCount+PoisonCount>4096)throw new ArgumentException("World object capacity exceeded.");}
    }
    public static class WorldMath {public static bool Finite(float n){return !float.IsNaN(n)&&!float.IsInfinity(n);} }
    public struct StableRandom
    {
        private uint _state; public StableRandom(uint seed){_state=seed==0?0x9e3779b9:seed;} public uint Next(){var x=_state;x^=x<<13;x^=x>>17;x^=x<<5;return _state=x;} public int Range(int min,int max){if(max<=min)throw new ArgumentException("Invalid random range.");return min+(int)(Next()%(uint)(max-min));}
    }
    public sealed class MapLayout
    {
        public string Version; public uint Seed; public int Size; public string Hash;
        public readonly List<WorldPoint> Spawns=new List<WorldPoint>(); public readonly List<WorldObjectDefinition> Objects=new List<WorldObjectDefinition>();
        public bool IsWalkable(WorldPoint p,float radius=.4f,Func<ushort,bool> broken=null,Func<ushort,bool> doorClosed=null)
        {if(!WorldMath.Finite(p.X)||!WorldMath.Finite(p.Y)||p.X<radius||p.Y<radius||p.X>Size-radius||p.Y>Size-radius)return false;foreach(var o in Objects)if((o.IsHardCover||(o.IsDestructible&&(broken==null||!broken(o.Id)))||(o.Kind==WorldObjectKind.Door&&doorClosed!=null&&doorClosed(o.Id)))&&o.Bounds.IntersectsCircle(p,radius))return false;return true;}
    }
    public struct WorldActor {public byte Id;public WorldPoint Position;public bool Alive;public WorldActor(byte id,float x,float y,bool alive=true){Id=id;Position=new WorldPoint(x,y);Alive=alive;} }
    public interface IEnvironmentDamageSink {void RequestDamage(byte actor,ushort source,uint tick,int amount,bool bypassArmor);}
    public interface IEnvironmentEffectSink {void ApplyEffect(byte actor,ushort source,string effect,uint tick,float seconds,float magnitude);void RemoveEffect(byte actor,ushort source,string effect,uint tick);}
    public interface IEnvironmentMovementSink {bool TryMove(byte actor,WorldPoint destination,ushort source,uint tick);}
}
