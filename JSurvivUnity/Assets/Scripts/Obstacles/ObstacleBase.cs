using System;
using System.Collections.Generic;
using System.Linq;
using _Codebase_.Entity.Player;
using Managers;
using UnityEngine;

[Serializable]
public enum ObstacleTagEnum
{
    Destructible,
    PullableRope,
    HeroHitByBatWillBeStunned,
    Trap,
    CanGoThrough,
    CanShootThrough
}

public abstract class ObstacleBase : MonoBehaviour
{
    [field: SerializeField]
    public string Id { get; set; }
    
    [field: SerializeField]
    public string Type { get; set; }
    
    [field: SerializeField]
    public ObstacleTagEnum[] Tags { get; set; }
    
    public Vector2 Position { get; protected set; }
    
    public List<Vector2> OccupiedPositions { get; protected set; } = new List<Vector2>();


    private void OnEnable()
    {
        MapManager.I.Obstacles.Add(this);
    }

    private void OnDisable()
    {
        MapManager.I.Obstacles.Remove(this);
    }

    public string[] GetTagsAsStringArray()
    {
        return this.Tags.Select(obstacleTagEnum => obstacleTagEnum.ToString()).ToArray();
    }

    protected virtual void Start()
    {
        // this.Position = Vector2Int.CeilToInt(transform.position) - Vector2.one * 0.5f;
        // if (this.OccupiedPositions.Count == 0)
        // {
        //     this.OccupiedPositions.Add(this.Position);
        // }
    }
    
    public virtual void CalculateOccupiedPositions()
    {
        this.Position = Vector2Int.CeilToInt(transform.position) - Vector2.one * 0.5f;
        this.OccupiedPositions.Clear();
        this.OccupiedPositions.Add(this.Position);
    }

    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;
        var playerEntity = collision.gameObject.GetComponentInParent<PlayerEntity>();

        this.OnPlayerEnter(playerEntity);
    }


    protected virtual void OnTriggerExit2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;
        var playerEntity = collision.gameObject.GetComponentInParent<PlayerEntity>();

        this.OnPlayerExit(playerEntity);
    }

    protected virtual void OnTriggerStay2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;
        var playerEntity = collision.gameObject.GetComponentInParent<PlayerEntity>();

        this.OnPlayerStay(playerEntity);
    }

    protected virtual void OnPlayerEnter(PlayerEntity playerEntity)
    {
    }

    protected virtual void OnPlayerExit(PlayerEntity playerEntity)
    {
    }

    protected virtual void OnPlayerStay(PlayerEntity playerEntity)
    {
    }
}