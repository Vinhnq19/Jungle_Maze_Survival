using System;
using Managers;
using UnityEngine;

namespace _Codebase_.Entity.Destructible
{
    public class BuildingEntity : MonoBehaviour
    {
        [field: SerializeField] public string Id { get; set; }

        public Vector2 Position => Vector2Int.CeilToInt(transform.position) - Vector2.one * 0.5f;

        private void OnEnable()
        {
            MapManager.I.Buildings.Add(this);
        }

        private void OnDisable()
        {
            MapManager.I.Buildings.Remove(this);
        }
    }
}