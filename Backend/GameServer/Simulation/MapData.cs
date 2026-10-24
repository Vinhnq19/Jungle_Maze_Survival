using System.Text.Json;

namespace GameServer.Simulation;

public readonly struct AABB
{
    public string Name { get; }
    public float MinX { get; }
    public float MaxX { get; }
    public float MinY { get; }
    public float MaxY { get; }

    public AABB(string name, float minX, float maxX, float minY, float maxY)
    {
        Name = name;
        MinX = minX;
        MaxX = maxX;
        MinY = minY;
        MaxY = maxY;
    }

    public bool Contains(float x, float y) =>
        x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
}

public sealed class MapData
{
    public string Version { get; set; } = "1.0.0";
    public float MinX { get; set; } = 0f;
    public float MaxX { get; set; } = 50f;
    public float MinY { get; set; } = 0f;
    public float MaxY { get; set; } = 50f;
    public List<AABB> Obstacles { get; } = new();
    public List<(float X, float Y)> SpawnPoints { get; } = new();

    public void Validate()
    {
        if (!float.IsFinite(MinX) || !float.IsFinite(MaxX) || !float.IsFinite(MinY) || !float.IsFinite(MaxY) ||
            MinX >= MaxX || MinY >= MaxY)
            throw new ArgumentException("Map bounds must be finite and min < max.");

        foreach (var obs in Obstacles)
        {
            if (!float.IsFinite(obs.MinX) || !float.IsFinite(obs.MaxX) || !float.IsFinite(obs.MinY) || !float.IsFinite(obs.MaxY) || obs.MinX < MinX || obs.MaxX > MaxX || obs.MinY < MinY || obs.MaxY > MaxY ||
                obs.MinX >= obs.MaxX || obs.MinY >= obs.MaxY)
                throw new ArgumentException($"Obstacle {obs.Name} is outside map bounds or inverted.");
        }

        if (SpawnPoints.Count < 10)
            throw new ArgumentException("At least 10 spawn points required.");

        for (var i = 0; i < 10; i++)
        {
            var p1 = SpawnPoints[i];
            if (!float.IsFinite(p1.X) || !float.IsFinite(p1.Y) || p1.X < MinX || p1.X > MaxX || p1.Y < MinY || p1.Y > MaxY)
                throw new ArgumentException($"Spawn point {i} ({p1.X}, {p1.Y}) is outside map bounds.");

            // Distance from initial center (25, 25) must be <= 25.0
            var distCenter = MathF.Sqrt(MathF.Pow(p1.X - 25f, 2) + MathF.Pow(p1.Y - 25f, 2));
            if (distCenter > 25.0f)
                throw new ArgumentException($"Spawn point {i} is outside initial zone circle.");

            // Must not overlap any obstacle with clearance 0.4
            foreach (var obs in Obstacles)
            {
                if (p1.X >= obs.MinX - 0.4f && p1.X <= obs.MaxX + 0.4f &&
                    p1.Y >= obs.MinY - 0.4f && p1.Y <= obs.MaxY + 0.4f)
                    throw new ArgumentException($"Spawn point {i} collides with obstacle {obs.Name}.");
            }

            // Pairwise distance >= 6.0
            for (var j = i + 1; j < 10; j++)
            {
                var p2 = SpawnPoints[j];
                var dist = MathF.Sqrt(MathF.Pow(p1.X - p2.X, 2) + MathF.Pow(p1.Y - p2.Y, 2));
                if (dist < 6.0f - 1e-3f)
                    throw new ArgumentException($"Spawn points {i} and {j} distance {dist:F2} < 6.0.");
            }
        }
    }

    public bool IsWalkable(float x,float y,float radius=.4f)
    {
        if(!float.IsFinite(x)||!float.IsFinite(y)||x<MinX+radius||x>MaxX-radius||y<MinY+radius||y>MaxY-radius)return false;
        foreach(var b in Obstacles)
        {
            var cx=Math.Clamp(x,b.MinX,b.MaxX);var cy=Math.Clamp(y,b.MinY,b.MaxY);
            if((x-cx)*(x-cx)+(y-cy)*(y-cy)<radius*radius)return false;
        }
        return true;
    }
    public HashSet<(int X,int Y)> ReachableCells(float radius=.4f)
    {
        if(SpawnPoints.Count==0)throw new ArgumentException("Map needs spawn points.");
        var first=((int)MathF.Floor(SpawnPoints[0].X),(int)MathF.Floor(SpawnPoints[0].Y));
        var result=new HashSet<(int X,int Y)>();var queue=new Queue<(int X,int Y)>();
        if(!IsWalkable(first.Item1+.5f,first.Item2+.5f,radius))throw new ArgumentException("Spawn is not connected to walkable grid.");
        result.Add(first);queue.Enqueue(first);
        var dirs=new[]{(1,0),(-1,0),(0,1),(0,-1)};
        while(queue.TryDequeue(out var p))foreach(var d in dirs)
        {
            var n=(p.X+d.Item1,p.Y+d.Item2);
            if(!result.Contains(n)&&IsWalkable(n.Item1+.5f,n.Item2+.5f,radius)){result.Add(n);queue.Enqueue(n);}
        }
        foreach(var spawn in SpawnPoints)
            if(!result.Contains(((int)MathF.Floor(spawn.X),(int)MathF.Floor(spawn.Y))))throw new ArgumentException("Spawns are in disconnected components.");
        return result;
    }

    public static MapData Load(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var map = new MapData { Version = root.GetProperty("version").GetString() ?? throw new ArgumentException("Missing map version.") };

        if (root.TryGetProperty("mapBounds", out var bounds))
        {
            map.MinX = (float)bounds.GetProperty("minX").GetDouble();
            map.MaxX = (float)bounds.GetProperty("maxX").GetDouble();
            map.MinY = (float)bounds.GetProperty("minY").GetDouble();
            map.MaxY = (float)bounds.GetProperty("maxY").GetDouble();
        }

        if (root.TryGetProperty("obstacles", out var obstacles))
        {
            foreach (var elem in obstacles.EnumerateArray())
            {
                var name = elem.GetProperty("name").GetString() ?? "Obstacle";
                var minX = (float)elem.GetProperty("minX").GetDouble();
                var maxX = (float)elem.GetProperty("maxX").GetDouble();
                var minY = (float)elem.GetProperty("minY").GetDouble();
                var maxY = (float)elem.GetProperty("maxY").GetDouble();
                map.Obstacles.Add(new AABB(name, minX, maxX, minY, maxY));
            }
        }

        if (root.TryGetProperty("spawnPoints", out var spawns))
        {
            foreach (var elem in spawns.EnumerateArray())
            {
                var x = (float)elem.GetProperty("x").GetDouble();
                var y = (float)elem.GetProperty("y").GetDouble();
                map.SpawnPoints.Add((x, y));
            }
        }

        map.Validate();
        return map;
    }
}
