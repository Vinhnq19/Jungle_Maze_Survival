using JungleMaze.App.Player;
using JungleMaze.App.Ports;
using JungleMaze.Core.DI;
using JungleMaze.Core.Pooling;
using UnityEngine;

namespace JungleMaze.Presentation.World
{
    /// <summary>
    /// Pooled local acceptance projectile. Sweep the complete step so even a fast shot cannot tunnel
    /// through a thin wall. Hit or range exhaustion returns the object to the pool, without damage/network.
    /// </summary>
    public sealed class CoverProbeProjectile : MonoBehaviour, IPoolable
    {
        // Encloses the entire 0.18 x 0.08 probe sprite, including its leading edge.
        private const float Radius = .1f;
        private IPoolService _pool;
        private Vector2 _direction;
        private float _speed, _remaining;
        private bool _flying;
        public Collider2D LastHit { get; private set; }
        public float Damage { get; private set; }
        public float Speed => _speed;
        public float RemainingRange => _remaining;
        public System.Action<Vector3> HitEffect { private get; set; }

        [Inject]
        public void Construct(IPoolService pool) => _pool = pool;

        public void Launch(in ConfiguredShot shot, Vector2 direction)
        {
            _direction = direction.normalized;
            _speed = shot.Speed;
            _remaining = shot.Range;
            Damage = shot.Damage;
            _flying = true;
        }

        private void FixedUpdate() => Advance(Time.fixedDeltaTime);

        public void Advance(float deltaTime)
        {
            if (!_flying || deltaTime <= 0) return;
            var step = Mathf.Min(_remaining, _speed * deltaTime);
            var start = (Vector2)transform.position;
            var hit = CoverQuery.Sweep(start, _direction, step, Radius);
            if (hit.collider != null)
            {
                LastHit = hit.collider;
                transform.position = hit.centroid;
                HitEffect?.Invoke(hit.point);
                Finish();
                return;
            }
            transform.position = start + _direction * step;
            _remaining -= step;
            if (_remaining <= .00001f) Finish();
        }

        private void Finish()
        {
            _flying = false;
            _pool.Despawn(this);
        }

        public void OnSpawned() { _flying = false; LastHit = null; HitEffect = null; }
        public void OnDespawned() { _flying = false; HitEffect = null; }
    }
}
