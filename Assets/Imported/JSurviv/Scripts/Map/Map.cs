using _Codebase_.Configs;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Map
{
    public class Map : MonoBehaviour
    {
        [Header("Map Info")]
        public MapTag mapTag;
        public bool isLargeMap;

        public Tilemap      ground;
        public Tilemap      indestructibles;
        public BorderSystem borderSystem;

        [Space(10)]
        [SerializeField]
        public MapConfig mapConfig;

    }

    public enum MapTag
    {
        Park,
        House,
        Terrace,
        Test,
    }
}