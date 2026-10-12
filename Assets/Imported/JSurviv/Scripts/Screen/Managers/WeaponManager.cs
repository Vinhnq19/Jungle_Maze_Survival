using System;
using System.Collections.Generic;
using System.Linq;
using _Codebase_.Entity.Weapon;
using _Codebase_.Utils;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Managers
{
    public class WeaponManager : MonoBehaviour
    {
        [field: SerializeField]
        private BaseWeaponEntity[] WeaponPrefabs { get; set; }

        private readonly List<BaseWeaponEntity> _weapons = new();

        public List<BaseWeaponEntity> Weapons
        {
            get
            {
                _weapons.RemoveAll(weapon => weapon == null);
                return _weapons;
            }
        }
        public int SpawnWeaponTotal { get; set; }
        public int SpawnTime { get; set; }
        public int DecreaseAmount { get; set; }
        public int RemainingWeapons { get; set; }

        public void SetUp(int botAmount, Map.Map gameMap)
        {
            var config = gameMap.mapConfig;
            var n = botAmount;
            
            float totalSafeTime = config.TimeToStartBorder + ((config.NumberOfShrinks - 1) * config.ShrinkDelay);
            float totalSafeMinutes = totalSafeTime / 60f;
            
            this.SpawnWeaponTotal = Mathf.RoundToInt(n * config.WeaponsPerBotPerMinute * totalSafeMinutes);

            this.SpawnTime = config.NumberOfWeaponSpawnWaves;
            
            var X = this.SpawnTime;
            var Y = this.SpawnWeaponTotal;
    
            if (X > 1)
            {
                this.DecreaseAmount = (int)Mathf.Floor((2f * Y / X - 2f * n) / (X - 1));
            }
            else
            {
                this.DecreaseAmount = 0;
            }
    
            this.RemainingWeapons = this.SpawnWeaponTotal;
    
        }

        public BaseWeaponEntity SpawnWeapon(BaseWeaponEntity prefab, Vector2 position)
        {
            var weapon = Instantiate(prefab, position, Quaternion.identity);
            weapon.transform.localScale = Vector3.one;
            weapon.transform.SetParent(this.transform);
            return weapon;
        }

        public List<Vector2> SpawnWithBotAmount(WeaponType type, int botAmount, List<Vector2> excludingPositions = null)
        {
            List<Vector2> weaponListPosition = new();
            List<Vector2> newlySpawnedPositions = new();

            if (excludingPositions != null)
            {
                newlySpawnedPositions.AddRange(excludingPositions);
            }
    
            var spawnAmountEachTime = botAmount / 2 + (this.SpawnTime - 1) * this.DecreaseAmount;

            while (weaponListPosition.Count < spawnAmountEachTime)
            {
                var randomPos = MapManager.I.GetSafeSpawnPosition(newlySpawnedPositions);

                if (randomPos != new Vector2(35.5f, 35.5f))
                {
                    var prefab = this.WeaponPrefabs.Where(w => w.GetWeaponType() == type).ToArray().RandomWithRarity();
                    this.SpawnWeapon(prefab, randomPos);

                    var gridPos = (Vector2)Vector2Int.FloorToInt(randomPos);
                    weaponListPosition.Add(gridPos);
                    newlySpawnedPositions.Add(gridPos);
                }
                else
                {
                    break; 
                }
            }

            this.SpawnTime--;
            this.RemainingWeapons -= spawnAmountEachTime;
            return weaponListPosition;
        }

        public List<Vector2> SpawnWithWeaponAmount(WeaponType? type, int weaponAmount = 1, List<Vector2> excludingPositions = null)
        {
            List<Vector2> weaponListPosition = new();
            List<Vector2> newlySpawnedPositions = new();

            if (excludingPositions != null)
            {
                newlySpawnedPositions.AddRange(excludingPositions);
            }

            while (weaponListPosition.Count < weaponAmount)
            {
                var randomPos = MapManager.I.GetSafeSpawnPosition(newlySpawnedPositions);
        
                if (randomPos != new Vector2(35.5f, 35.5f))
                {
                    var prefab = this.WeaponPrefabs.Where(w => !type.HasValue || w.GetWeaponType() == type.Value).ToArray().RandomWithRarity();
                    var weapon = this.SpawnWeapon(prefab, randomPos);

                    var gridPos = (Vector2)Vector2Int.FloorToInt(randomPos);
                    weaponListPosition.Add(gridPos);
                    newlySpawnedPositions.Add(gridPos);
                }
                else
                {
                    break;
                }
            }

            return weaponListPosition;
        }
        
        public void DeSpawnAllWeapons()
        {
            for (int i = _weapons.Count - 1; i >= 0; i--)
            {
                if (_weapons[i] != null)
                {
                    Destroy(_weapons[i].gameObject);
                }
            }
            _weapons.Clear();

            SpawnWeaponTotal = 0;
            SpawnTime = 0;
            DecreaseAmount = 0;
            RemainingWeapons = 0;

            Debug.Log("WeaponManager: All weapons have been despawned and state has been reset.");
        }

        #region Singleton

        public static WeaponManager I { get; private set; }

        private void Awake()
        {
            I = this;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void RuntimeInitializeOnLoadMethod()
        {
            I = null;
        }

        #endregion
    }
}