using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CompoundObstacle : ObstacleBase
{
    public override void CalculateOccupiedPositions()
    {
        base.Start();

        this.OccupiedPositions.Clear();

        var colliders = GetComponentsInChildren<Collider2D>();

        if (colliders.Length == 0) return;

        var totalBounds = colliders[0].bounds;
        foreach (var coll in colliders.Skip(1))
        {
            totalBounds.Encapsulate(coll.bounds);
        }

        var min = Vector2Int.FloorToInt(totalBounds.min);
        var max = Vector2Int.CeilToInt(totalBounds.max);

        for (int x = min.x; x < max.x; x++)
        {
            for (int y = min.y; y < max.y; y++)
            {
                var cellCenter = new Vector2(x + 0.5f, y + 0.5f);

                foreach (var coll in colliders)
                {
                    if (coll.OverlapPoint(cellCenter))
                    {
                        this.OccupiedPositions.Add(new Vector2(x, y));
                        break; 
                    }
                }
            }
        }
    }
}