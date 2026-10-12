using Managers;
using UnityEngine;

namespace _Codebase_.Entity.Indestructible
{
    public class Indestructible : MonoBehaviour
    {
        [SerializeField]
        private Vector2[] _tookPositions;

        public Vector2[] Positions { get; private set; }

        private void Start()
        {
            var originX = this.transform.position.x - .5f;
            var originY = this.transform.position.y - .5f;

            this.Positions = new Vector2[this._tookPositions.Length];

                for (var i = 0; i < this._tookPositions.Length; i++)
            {
                this.Positions[i] = new(
                    originX + this._tookPositions[i].x,
                    originY + this._tookPositions[i].y
                );
            }

            MapManager.I.IndestructiblePositions.AddRange(this.Positions);
        }

        private void OnDrawGizmosSelected()
        {
            if (this._tookPositions == null || this._tookPositions.Length == 0) return;

            var originX = this.transform.position.x;
            var originY = this.transform.position.y;

            Gizmos.color = Color.green;

            foreach (var pos in this._tookPositions)
            {
                Gizmos.DrawWireCube(
                    new(originX + pos.x, originY + pos.y, 0),
                    Vector3.one
                );
            }
        }
    }
}