using JungleMaze.App.Ports;
using JungleMaze.Core.DI;
using UnityEngine;

namespace JungleMaze.Presentation.Player
{
    /// <summary>Follow the interpolated Hero in LateUpdate; keep the viewport inside the map.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class HeroCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        private Camera _camera;
        private float _mapSize = 50f;

        public Transform Target { get => target; set => target = value; }

        [Inject]
        public void Construct(IBalanceConfigProvider balance) => _mapSize = balance.Match.MapSizeCells;

        private void Awake() => _camera = GetComponent<Camera>();

        private void LateUpdate()
        {
            if (target == null) return;
            var halfHeight = _camera.orthographicSize;
            var halfWidth = halfHeight * _camera.aspect;
            var p = target.position;
            p.x = halfWidth * 2 >= _mapSize ? _mapSize / 2 : Mathf.Clamp(p.x, halfWidth, _mapSize - halfWidth);
            p.y = halfHeight * 2 >= _mapSize ? _mapSize / 2 : Mathf.Clamp(p.y, halfHeight, _mapSize - halfHeight);
            p.z = transform.position.z;
            transform.position = p;
        }
    }
}
