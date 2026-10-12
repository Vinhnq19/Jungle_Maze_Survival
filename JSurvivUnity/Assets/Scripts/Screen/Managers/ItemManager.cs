using System.Collections.Generic;
using _Codebase_.Entity.Armor;
using _Codebase_.Entity.Item;
using _Codebase_.Utils;
using JetBrains.Annotations;
using UnityEngine;

namespace Managers
{
    public class ItemManager : MonoBehaviour
    {
        [field: SerializeField]
        private BaseItemEntity[] ItemsPrefab { get; set; }

        private readonly List<BaseItemEntity> _items = new();
        private readonly List<ArmorEntity> _armors = new();

        public List<BaseItemEntity> Items
        {
            get
            {
                _items.RemoveAll(item => item == null);
                return _items;
            }
        }

        public List<ArmorEntity> Armors
        {
            get
            {
                _armors.RemoveAll(armor => armor == null);
                return _armors;
            }
        }

        public List<Vector2> RandomSpawnItems(int count, [CanBeNull] List<Vector2> excludingPosList = null)
        {
            var itemListPosition = new List<Vector2>();

            for (var i = 0; i < count; i++)
            {
                List<Vector2> allExcludingPosLists = new();
                allExcludingPosLists.AddRange(excludingPosList ?? new List<Vector2>());
                allExcludingPosLists.AddRange(itemListPosition);

                var prefab = this.ItemsPrefab.RandomWithRarity();
                var spawnPosition = MapManager.I.GetRandomGroundPosExcludingPositions(allExcludingPosLists);

                var item = Instantiate(
                    prefab,
                    spawnPosition,
                    Quaternion.identity
                );
                
                this.Items.Add(item);
                item.transform.SetParent(this.transform);
                itemListPosition.Add(item.gameObject.transform.position);
            }

            return itemListPosition;
        }
        
        public void DeSpawnAllItems()
        {
            // Dùng _items và _armors để thao tác trực tiếp
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                if (_items[i] != null)
                {
                    Destroy(_items[i].gameObject);
                }
            }
            _items.Clear();

            for (int i = _armors.Count - 1; i >= 0; i--)
            {
                if (_armors[i] != null)
                {
                    Destroy(_armors[i].gameObject);
                }
            }
            _armors.Clear();
            
            Debug.Log("ItemManager: All items and armors have been despawned.");
        }

        #region Singleton

        public static ItemManager I { get; private set; }

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