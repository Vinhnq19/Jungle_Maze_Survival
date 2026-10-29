#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
namespace JungleMaze.World
{
    public static class MapLayoutGenerator
    {
        public static MapLayout Generate(MapLayoutConfig config,uint seed)
        {
            config.Validate();var m=new MapLayout{Version=config.Version,Seed=seed,Size=config.Size};var rng=new StableRandom(seed);
            // Keep initial spawns inside the active legacy r25 schedule as well as proposed r36.
            // Ring spacing remains >= 6 at the supported minimum map size of 30.
            var spawnRadius=Math.Min(config.Size*.36f,config.InitialZoneRadius-.5f);
            for(var i=0;i<10;i++){var angle=i*Math.PI*2/10;m.Spawns.Add(new WorldPoint(config.Size/2f+(float)Math.Cos(angle)*spawnRadius,config.Size/2f+(float)Math.Sin(angle)*spawnRadius));}
            var cells=new List<WorldPoint>();for(var y=1;y<config.Size-1;y++)for(var x=1;x<config.Size-1;x++)cells.Add(new WorldPoint(x+.5f,y+.5f));
            for(var i=cells.Count-1;i>0;i--){var j=rng.Range(0,i+1);var p=cells[i];cells[i]=cells[j];cells[j]=p;}
            Place(m,config,cells,WorldObjectKind.Rock,1,4,4);Place(m,config,cells,WorldObjectKind.Statue,1,3,3);Place(m,config,cells,WorldObjectKind.Pillar,4,1,2);Place(m,config,cells,WorldObjectKind.Wall,4,1,4);
            Place(m,config,cells,WorldObjectKind.Swamp,config.SwampCount,4,4);Place(m,config,cells,WorldObjectKind.Lava,config.LavaCount,3,3);Place(m,config,cells,WorldObjectKind.Poison,config.PoisonCount,1,1);
            Place(m,config,cells,WorldObjectKind.Door,config.DoorCount,1,2);Place(m,config,cells,WorldObjectKind.Portal,config.PortalPairs*2,1,1);var portal=0;foreach(var o in m.Objects)if(o.Kind==WorldObjectKind.Portal)o.PairId=(ushort)((++portal+1)/2);
            Place(m,config,cells,WorldObjectKind.Chest,config.ChestCount,1,1);Place(m,config,cells,WorldObjectKind.Pot,config.PotCount,1,1);Place(m,config,cells,WorldObjectKind.Bush,config.BushCount,1.5f,1.5f);
            Validate(m,config);m.Hash=ComputeHash(m);return m;
        }
        private static void Place(MapLayout m,MapLayoutConfig c,List<WorldPoint> cells,WorldObjectKind kind,int count,float w,float h)
        {
            var placed=0;foreach(var p in cells)
            {
                if(placed==count)break;var b=new WorldBounds(p.X,p.Y,w,h);if(b.MinX<1||b.MinY<1||b.MaxX>m.Size-1||b.MaxY>m.Size-1)continue;
                // Reserve a one-cell connected road lattice. These remain open even with intact containers.
                var road=false;for(var x=(int)Math.Floor(b.MinX);x<Math.Ceiling(b.MaxX);x++)if(x%10==4)road=true;for(var y=(int)Math.Floor(b.MinY);y<Math.Ceiling(b.MaxY);y++)if(y%10==4)road=true;if(road)continue;
                var overlap=false;foreach(var spawn in m.Spawns)if(b.Contains(spawn,1.25f))overlap=true;foreach(var old in m.Objects)if(b.Overlaps(old.Bounds,.05f))overlap=true;if(overlap)continue;
                m.Objects.Add(new WorldObjectDefinition{Id=(ushort)(m.Objects.Count+1),Kind=kind,Bounds=b});placed++;
            }
            if(placed!=count)throw new ArgumentException("Cannot place requested "+kind+" count; change map size/counts or seed.");
        }
        public static void Validate(MapLayout m,MapLayoutConfig c)
        {
            c.Validate();if(m.Version!=c.Version||m.Size!=c.Size||m.Spawns.Count!=c.SpawnCount)throw new ArgumentException("Map identity or spawn count mismatch.");var ids=new HashSet<ushort>();var pairs=new Dictionary<ushort,int>();
            foreach(var o in m.Objects){if(o.Id==0||!ids.Add(o.Id)||!Enum.IsDefined(typeof(WorldObjectKind),o.Kind)||!WorldMath.Finite(o.Bounds.MinX)||!WorldMath.Finite(o.Bounds.MinY)||!WorldMath.Finite(o.Bounds.MaxX)||!WorldMath.Finite(o.Bounds.MaxY)||o.Bounds.MinX<0||o.Bounds.MinY<0||o.Bounds.MaxX>m.Size||o.Bounds.MaxY>m.Size||o.Bounds.MinX>=o.Bounds.MaxX||o.Bounds.MinY>=o.Bounds.MaxY)throw new ArgumentException("Invalid world object.");if(o.Kind==WorldObjectKind.Portal){if(o.PairId==0)throw new ArgumentException("Portal pair missing.");pairs[o.PairId]=pairs.ContainsKey(o.PairId)?pairs[o.PairId]+1:1;}}
            foreach(var n in pairs.Values)if(n!=2)throw new ArgumentException("Portal pair must have two endpoints.");
            for(var i=0;i<m.Objects.Count;i++)for(var j=i+1;j<m.Objects.Count;j++)if(m.Objects[i].Bounds.Overlaps(m.Objects[j].Bounds))throw new ArgumentException("World objects overlap.");
            for(var i=0;i<m.Spawns.Count;i++){var p=m.Spawns[i];if(!m.IsWalkable(p,c.HeroRadius))throw new ArgumentException("Spawn blocked.");foreach(var o in m.Objects)if(o.IsHazard&&o.Bounds.Contains(p,c.HeroRadius))throw new ArgumentException("Spawn on hazard.");var center=new WorldPoint(m.Size/2f,m.Size/2f);if(p.DistanceSquared(center)>c.InitialZoneRadius*c.InitialZoneRadius)throw new ArgumentException("Spawn outside configured initial zone.");for(var j=i+1;j<m.Spawns.Count;j++)if(p.DistanceSquared(m.Spawns[j])<c.SpawnMinDistance*c.SpawnMinDistance)throw new ArgumentException("Spawns too close.");}
            var counts=new Dictionary<WorldObjectKind,int>();foreach(var o in m.Objects)counts[o.Kind]=counts.ContainsKey(o.Kind)?counts[o.Kind]+1:1;CheckCount(counts,WorldObjectKind.Chest,c.ChestCount);CheckCount(counts,WorldObjectKind.Pot,c.PotCount);CheckCount(counts,WorldObjectKind.Bush,c.BushCount);CheckCount(counts,WorldObjectKind.Portal,c.PortalPairs*2);
            var visited=new HashSet<int>();var q=new Queue<int>();var first=(int)m.Spawns[0].X+(int)m.Spawns[0].Y*m.Size;q.Enqueue(first);visited.Add(first);while(q.Count>0){var cell=q.Dequeue();var x=cell%m.Size;var y=cell/m.Size;foreach(var d in new[]{-1,1,-m.Size,m.Size}){var n=cell+d;var nx=n%m.Size;var ny=n/m.Size;if(n<0||n>=m.Size*m.Size||Math.Abs(nx-x)+Math.Abs(ny-y)!=1||visited.Contains(n)||!m.IsWalkable(new WorldPoint(nx+.5f,ny+.5f),c.HeroRadius))continue;visited.Add(n);q.Enqueue(n);}}
            foreach(var p in m.Spawns)if(!visited.Contains((int)p.X+(int)p.Y*m.Size))throw new ArgumentException("Spawns are disconnected.");
        }
        private static void CheckCount(Dictionary<WorldObjectKind,int> counts,WorldObjectKind kind,int expected){if((counts.ContainsKey(kind)?counts[kind]:0)!=expected)throw new ArgumentException("World object count mismatch: "+kind);}
        public static string ComputeHash(MapLayout m){var b=new StringBuilder().Append(m.Version).Append('|').Append(m.Seed).Append('|').Append(m.Size);foreach(var p in m.Spawns)AppendPoint(b,p);foreach(var o in m.Objects){b.Append('|').Append(o.Id).Append(':').Append((byte)o.Kind).Append(':').Append(o.PairId);AppendPoint(b,new WorldPoint(o.Bounds.MinX,o.Bounds.MinY));AppendPoint(b,new WorldPoint(o.Bounds.MaxX,o.Bounds.MaxY));}using(var sha=SHA256.Create()){var hash=sha.ComputeHash(Encoding.UTF8.GetBytes(b.ToString()));var result=new StringBuilder();foreach(var v in hash)result.Append(v.ToString("x2"));return result.ToString();}}
        private static void AppendPoint(StringBuilder b,WorldPoint p){b.Append('|').Append(p.X.ToString("R",CultureInfo.InvariantCulture)).Append(',').Append(p.Y.ToString("R",CultureInfo.InvariantCulture));}
    }
}
