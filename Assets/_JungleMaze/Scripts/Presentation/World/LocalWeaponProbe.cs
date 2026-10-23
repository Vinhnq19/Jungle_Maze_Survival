using JungleMaze.App.Player;
using JungleMaze.App.Pooling;
using JungleMaze.App.Ports;
using JungleMaze.Core.DI;
using UnityEngine;

namespace JungleMaze.Presentation.World
{
    /// <summary>
    /// Local acceptance harness: hold left mouse to inspect JSON-driven speed/range/cadence and cover.
    /// Uses the real pool/factory for projectiles AND a short impact visual; no combat or multiplayer claims.
    /// </summary>
    public sealed class LocalWeaponProbe : MonoBehaviour
    {
        [SerializeField] private Transform hero;
        [SerializeField] private CoverProbeProjectile projectilePrefab;
        [SerializeField] private Transform impactPrefab;
        [SerializeField] private string weaponId = "RUBBER_GUN";
        private IBalanceConfigProvider _balance;
        private IPoolService _pool;
        private PooledPrefabFactory<CoverProbeProjectile> _projectiles;
        private PooledPrefabFactory<Transform> _impacts;
        private System.Action<Vector3> _onHit;
        private float _nextShot;
        public string WeaponId { get => weaponId; set => weaponId = value; }

        [Inject]
        public void Construct(IBalanceConfigProvider balance, IPoolService pool)
        {
            _balance = balance;
            _pool = pool;
        }

        private void Start()
        {
            if (_pool == null || projectilePrefab == null || impactPrefab == null || hero == null)
            { enabled = false; return; }
            _projectiles = new PooledPrefabFactory<CoverProbeProjectile>(_pool, projectilePrefab, 16);
            _impacts = new PooledPrefabFactory<Transform>(_pool, impactPrefab, 8);
            _onHit = SpawnImpact; // Cache delegate once, not on each projectile/hit.
        }

        private void Update()
        {
            if (!Input.GetMouseButton(0) || Time.time < _nextShot) return;
            if (!_balance.TryGetWeapon(weaponId, out var weapon) || weapon.Category != JungleMaze.Domain.Balance.WeaponCategory.Ranged) return;
            var shot = new ConfiguredShot(_balance, weaponId);
            var angle = hero.eulerAngles.z * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            // Spawn at Hero centre: cover immediately beside the muzzle cannot be skipped.
            var pose = new SpawnPose(hero.position, Quaternion.Euler(0, 0, hero.eulerAngles.z));
            var projectile = _projectiles.Create(in pose);
            projectile.HitEffect = _onHit;
            projectile.Launch(in shot, direction);
            _nextShot = Time.time + shot.Interval;
        }

        private void SpawnImpact(Vector3 position)
        {
            var pose = new SpawnPose(position, Quaternion.identity);
            var impact = _impacts.Create(in pose);
            _pool.Despawn(impact, .12f);
        }
    }
}
