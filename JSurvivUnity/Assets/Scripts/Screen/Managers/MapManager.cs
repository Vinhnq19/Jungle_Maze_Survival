using System.Collections.Generic;
using System.Linq;
using _Codebase_.Entity.Destructible;
using _Codebase_.Entity.Enemy;
using _Concrete_.Entities.CanGoThrough;
using JetBrains.Annotations;
using UnityEngine;

namespace Managers
{
    public class MapManager : MonoBehaviour
    {
        public float MapTime;

        [SerializeField] private Map.Map _map;
        
        // xin loi vi da fix cung nhung ma ko con nhieu thoi gian
        [Header("Hardcore Map Configs hẹ hẹ hẹ")]
        [SerializeField] private MapConfig config70Map5min;
        [SerializeField] private MapConfig config70Map10min;

        public Map.Map Map
        {
            get => this._map;
            set => this._map = value;
        }

        public List<Vector2> IndestructiblePositions { get; set; }

        private List<DestructibleEntity> _destructibles = new();
        private List<EnemyEntity> _enemies = new();
        private HashSet<ObstacleBase> _obstacles = new();

        public List<DestructibleEntity> Destructibles
        {
            get
            {
                _destructibles.RemoveAll(d => d == null);
                return _destructibles;
            }
        }
        
        public List<EnemyEntity> Enemies
        {
            get
            {
                _enemies.RemoveAll(e => e == null);
                return _enemies;
            }
        }
        
        public HashSet<ObstacleBase> Obstacles
        {
            get
            {
                _obstacles.RemoveWhere(o => o == null);
                return _obstacles;
            }
        }

        //Them list canGoThroughEntity
        // public List<Bush> CanGoThroughs { get; set; }
        public HashSet<BuildingEntity> Buildings { get; } = new();
        // public HashSet<ObstacleBase> Obstacles { get; } = new();
        // public List<EnemyEntity> Enemies { get; set; }
        public static MapManager I { get; set; }


        private void Awake()
        {
            this.IndestructiblePositions = new();
            this._destructibles = new();
            this._enemies = new();
            this._obstacles = new();
            I = this;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void RuntimeInitializeOnLoadMethod()
        {
            I = null;
        }

        public Vector2 GetRandomGroundPosExcludingPositions(List<Vector2> excludingPositions = null)
        {
            var filteredPos = this.GetAllGroundPosWithExcludingPositions(excludingPositions);
            return filteredPos[Random.Range(0, filteredPos.Count)] + new Vector2(0.5f, 0.5f);
        }

        public Vector2 GetRandomAvailablePosition(List<Vector2> excludingPositions = null)
        {
            var playerPositions = PlayerManager.I.Players.Select(player => player.Position).ToList();

            //get all the position of spawned weapons
            var weaponPositions = WeaponManager.I.Weapons.Select(weapon => weapon.Position).ToList();

            // get all the position of items
            var itemPositions = ItemManager.I.Items
                .Select(item => (Vector2)(item.transform.position - Vector3.one * 0.5f)).ToList();

            // get all the position of destructible objects
            var destructiblePositions =
                MapManager.I._destructibles.Select(destructible => destructible.Positions).ToList();

            List<Vector2> excludingPos = new();
            excludingPos.AddRange(playerPositions);
            excludingPos.AddRange(weaponPositions);
            excludingPos.AddRange(itemPositions);
            excludingPos.AddRange(destructiblePositions.SelectMany(pos => pos).ToList());

            List<Vector2> usedPositions = new();
            usedPositions.AddRange(excludingPositions ?? new List<Vector2>());


            var randomPos =
                MapManager.I.GetRandomGroundPositionInBorder(MapManager.I.Map.borderSystem.GetNextBorder(),
                    usedPositions);

            return randomPos;
        }

        public Vector2 GetRandomGroundPositionInBorder(List<Vector3> border, List<Vector2> excludingPositions = null)
        {
            // var allDestructiblePos = this.Destructibles.SelectMany(destructible => destructible.Position).ToList();
            // if (allDestructiblePos != null)
            // {
            //     excludingPositions.AddRange(allDestructiblePos);
            // }
            var allPos = this.GetAllGroundPosWithExcludingPositions(excludingPositions);

            while (true)
            {
                var pos = allPos[Random.Range(0, allPos.Count)] + new Vector2(0.5f, 0.5f);

                foreach (Vector3 bor in border)
                {
                    Debug.Log(bor + " vvvv");
                }

                if (border != null && border.Count >= 4)
                {
                    if (pos.x > border[0].x && pos.x < border[3].x &&
                        pos.y < border[1].y && pos.y > border[2].y)
                    {
                        return pos;
                    }
                }
                else
                {
                    Debug.LogError("Border: " + border.Count);
                    Debug.LogError("Border list không đủ 4 phần tử!");
                    return new Vector2(35.5f, 35.5f);
                }
            }
        }

        public List<Vector2> GetAllGroundPos()
        {
            var allGroundPos = new List<Vector2>();
            var cellBound = this._map.ground.cellBounds;

            var indestructibleSet = new HashSet<Vector2>(this.IndestructiblePositions);

            foreach (var pos in cellBound.allPositionsWithin)
            {
                if (this._map.ground.HasTile(pos))
                {
                    var groundPos = new Vector2(pos.x, pos.y);

                    if (!indestructibleSet.Contains(groundPos))
                    {
                        allGroundPos.Add(groundPos);
                    }
                }
            }

            return allGroundPos;
        }


        public List<Vector2> GetAllGroundPosWithExcludingPositions(List<Vector2> excludingPositions = null)
        {
            var allPos = this.GetAllGroundPos();
            var removePosSet = new HashSet<Vector2>();

            if (excludingPositions != null)
            {
                foreach (var pos in excludingPositions)
                {
                    removePosSet.Add(pos);
                }
            }

            return allPos.Where(pos => !removePosSet.Contains(pos)).ToList();
        }

        public List<Vector2> GetAllEnemyDangerZones()
        {
            var allDangerZones = new HashSet<Vector2>();
            foreach (var enemy in this._enemies)
            {
                var dangerZone = enemy.GetDangerZone();
                foreach (var pos in dangerZone)
                {
                    allDangerZones.Add(pos);
                }
            }

            return allDangerZones.ToList();
        }

        public List<Vector2> GetAllTrapPositions()
        {
            return this._obstacles
                .Where(o => o.Tags.Contains(ObstacleTagEnum.Trap))
                .Select(o => o.Position)
                .ToList();
        }

        public List<DragonEgg> DragonEggs { get; private set; } = new List<DragonEgg>();

        public void RegisterDragonEgg(DragonEgg egg)
        {
            if (!DragonEggs.Contains(egg))
            {
                DragonEggs.Add(egg);
            }
        }

        public void UnregisterDragonEgg(DragonEgg egg)
        {
            if (DragonEggs.Contains(egg))
            {
                DragonEggs.Remove(egg);
            }
        }

        public Vector2 GetRandomGroundPosition()
        {
            var allPos = this.GetAllGroundPos();
            return allPos[Random.Range(0, allPos.Count)] + new Vector2(0.5f, 0.5f);
        }

        public List<Vector2> GetAllIndestructiblePos()
        {
            return this.IndestructiblePositions;
        }

        public Vector2 GetSafeSpawnPosition([CanBeNull] List<Vector2> additionalExclusions = null)
        {
            var finalSafePositions = GetSafeSpawnPositionCandidates(additionalExclusions);

            if (finalSafePositions.Count > 0)
            {
                return finalSafePositions[Random.Range(0, finalSafePositions.Count)] + new Vector2(0.5f, 0.5f);
            }

            return new Vector2(35.5f, 35.5f); 
        }
        
        private List<Vector2> GetSafeSpawnPositionCandidates(List<Vector2> additionalExclusions = null)
        {
            var allGroundPos = this.GetAllGroundPos();
            var invalidPositions = new HashSet<Vector2>();

            foreach (var pos in this.IndestructiblePositions)
            {
                invalidPositions.Add(pos);
            }

            var neighborOffsets = new Vector2[]
            {
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(1, -1),
                new Vector2(0, -1), new Vector2(-1, -1), new Vector2(-1, 0), new Vector2(-1, 1)
            };

            foreach (var destructible in this._destructibles)
            {
                if (destructible.Positions != null && destructible.Positions.Count > 0)
                {
                    foreach (var pos in destructible.Positions)
                    {
                        invalidPositions.Add(pos);
                        foreach (var offset in neighborOffsets)
                        {
                            invalidPositions.Add(pos + offset);
                        }
                    }
                }
            }

            foreach (var player in PlayerManager.I.Players)
            {
                if (!player.IsDead)
                {
                    invalidPositions.Add(player.Position);
                }
            }
            
            foreach (var obstacle in this.Obstacles)
            {
                if (obstacle.OccupiedPositions != null)
                {
                    foreach (var pos in obstacle.OccupiedPositions)
                    {
                        invalidPositions.Add(pos);
                    }
                }
            }

            foreach (var weapon in WeaponManager.I.Weapons) 
            {
                if (weapon != null)
                {
                    invalidPositions.Add(weapon.Position);
                }
            }

            foreach (var item in ItemManager.I.Items)
            {
                invalidPositions.Add(Vector2Int.FloorToInt(item.transform.position));
            }
            if (additionalExclusions != null) { foreach (var pos in additionalExclusions) { invalidPositions.Add(pos); } }

            var candidatePositions = allGroundPos.Where(pos => !invalidPositions.Contains(pos)).ToList();

            var borderSystem = this.Map.borderSystem;
            if (borderSystem == null) return new List<Vector2>();

            if (borderSystem.CanShrinkFurther())
            {
                float nextWidth = borderSystem.GetCurrentWidth() - borderSystem.GetShrinkSize();
                Vector2 mapCenter = borderSystem.GetMapCenter();
                float halfNextWidth = nextWidth / 2f;
                float leftEdge = mapCenter.x - halfNextWidth;
                float rightEdge = mapCenter.x + halfNextWidth;
                float bottomEdge = mapCenter.y - halfNextWidth;
                float topEdge = mapCenter.y + halfNextWidth;

                return candidatePositions.Where(pos =>
                {
                    var worldPos = pos + new Vector2(0.5f, 0.5f);
                    return worldPos.x > leftEdge && worldPos.x < rightEdge &&
                           worldPos.y > bottomEdge && worldPos.y < topEdge;
                }).ToList();
            }
            else
            {
                return candidatePositions.Where(pos => borderSystem.IsPositionInSafeZone(pos)).ToList();
            }
        }
        
        public Vector2 GetSafeSpawnClusterPosition(Vector2Int[] offsets, List<Vector2> additionalExclusions = null)
        {
            var candidatePositions = this.GetSafeSpawnPositionCandidates(additionalExclusions);
            if (candidatePositions.Count == 0)
            {
                return new Vector2(35.5f, 35.5f);
            }
    
            var candidateSet = new HashSet<Vector2>(candidatePositions);

            candidatePositions = candidatePositions.OrderBy(x => Random.value).ToList();

            foreach (var anchorPos in candidatePositions)
            {
                bool isClusterValid = true;

                foreach (var offset in offsets)
                {
                    var checkPos = anchorPos + (Vector2)offset;

                    if (!candidateSet.Contains(checkPos))
                    {
                        isClusterValid = false;
                        break; 
                    }
                }

                if (isClusterValid)
                {
                    return anchorPos + new Vector2(0.5f, 0.5f);
                }
            }

            return new Vector2(35.5f, 35.5f);
        }


        public void DespawnMap()
        {
            foreach (Transform child in this.transform)
            {
                Destroy(child.gameObject);
            }

            this._map = null;

            this.IndestructiblePositions.Clear();
            this.Destructibles.Clear();
            this._enemies.Clear();
            this.Buildings.Clear();
            this._obstacles.Clear();
            
            Debug.Log("Map data has been fully cleared.");
        }

        public void ShowBorder()
        {
            if (this._map != null && this._map.borderSystem != null)
            {
                this._map.borderSystem.gameObject.SetActive(true);
            }
        }

        public void HideBorder()
        {
            if (this._map != null && this._map.borderSystem != null)
            {
                this._map.borderSystem.gameObject.SetActive(false);
            }
        }

        #region SetUp Map (Generate Indestructible Positions)


        public void SetUp()
        {
            if (this._map != null)
            {
                Destroy(this._map.gameObject);
            }
            
            this.IndestructiblePositions.Clear();
            this._destructibles.Clear();
            this._enemies.Clear();
            this.Buildings.Clear();
            this._obstacles.Clear();

            var map = Instantiate(LobbyManager.I.SelectedMap, this.transform);
            map.transform.localPosition = Vector3.zero;
            map.transform.localScale = Vector3.one;
            this._map = map;

            // fix cung map 70
            // todo: refactor later (maybe use a dictionary to store map configs)
            if (this._map.mapConfig.mapSize == 70)
            {
                if (this.MapTime < 360)
                {
                    this._map.mapConfig = config70Map5min;
                    Debug.Log("Applied Map Config: 70x70 - 5 min");
                }
                else
                {
                    this._map.mapConfig = config70Map10min;
                    Debug.Log("Applied Map Config: 70x70 - 10 min");
                }
            }
            /////////////////////////////////////
            
            this._destructibles = map.GetComponentsInChildren<DestructibleEntity>(true).ToList();
    
            foreach (var destructible in this._destructibles)
            {
                if (destructible.GetComponentInParent<DragonEgg>() != null)
                {
                    continue; 
                }

                var gridPos = (Vector2)Vector2Int.FloorToInt(destructible.transform.position);
                destructible.Positions.Clear();
                destructible.Positions.Add(gridPos);
            }
    
            this._destructibles.RemoveAll(d => d.GetComponentInParent<DragonEgg>() != null);


            this.GenerateObstaclesFromTilemap();

            if (this._map.borderSystem != null)
            {
                this._map.borderSystem.Initialize();
            }
            foreach (var obstacle in this.Obstacles)
            {
                if (obstacle != null)
                {
                    obstacle.CalculateOccupiedPositions();
                }
            }
        }

        public void GenerateObstaclesFromTilemap()
        {
            var cellBound = this._map.indestructibles.cellBounds;
            var indestructibleTilemap = this._map.indestructibles;

            foreach (var pos in cellBound.allPositionsWithin)
            {
                if (indestructibleTilemap.HasTile(pos))
                {
                    Vector2 worldPosForList = new Vector2(pos.x, pos.y);

                    this.IndestructiblePositions.Add(worldPosForList);

                    Vector2 worldPosForObject = worldPosForList + new Vector2(0.5f, 0.5f);
                    var obstacleObj = new GameObject($"Indestructible_Tile_{pos.x}_{pos.y}");
                    obstacleObj.transform.position = worldPosForObject;
                    obstacleObj.transform.SetParent(this.transform);

                    var basicObstacle = obstacleObj.AddComponent<BasicObstacle>();
                    basicObstacle.Id = "INDESTRUCTIBLE";
                    basicObstacle.Type = "OBSTACLE";
                    basicObstacle.Tags = new ObstacleTagEnum[1];

                    var collider = obstacleObj.AddComponent<BoxCollider2D>();
                    collider.size = Vector2.one;
                }
            }
        }

        public void GenerateIndestructiblePositions()
        {
            var cellBound = this._map.indestructibles.cellBounds;

            foreach (var pos in cellBound.allPositionsWithin)
            {
                if (this._map.indestructibles.HasTile(pos))
                {
                    this.IndestructiblePositions.Add(new(pos.x, pos.y));
                }
            }
        }

        public List<Vector2> GenerateDestructiblePositions()
        {
            var destructiblePositions = new List<Vector2>();

            foreach (var destructible in this._destructibles)
            {
                foreach (var pos in destructible.Positions)
                {
                    destructiblePositions.Add(pos);
                }
            }

            return destructiblePositions;
        }

        #endregion
    }
}