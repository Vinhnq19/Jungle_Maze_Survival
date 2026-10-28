using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Combat;
using JungleMaze.App.Combat.Mocks;
using JungleMaze.App.Hud.State;
using JungleMaze.App.Pooling;
using JungleMaze.App.Player;
using JungleMaze.App.Ports;
using JungleMaze.Core.DI;
using JungleMaze.Domain.Balance;
using JungleMaze.Presentation.Player;
using JungleMaze.Presentation.World;
using UnityEngine;

namespace JungleMaze.Presentation.Combat
{
    /// <summary>
    /// WPN-01 local demo: weapon keys, aim and pooled visuals on top of a <see cref="WeaponController"/> wired to the
    /// in-process fake combat backend. Shots and swings show as soon as the attack is predicted; ammo and reload come
    /// from the controller. Damage, HP and loot are never decided here. Disabled during networked matches.
    /// </summary>
    public sealed class CombatWeaponPresenter : MonoBehaviour
    {
        private const byte LocalNetId = 0;

        private static readonly KeyCode[] SelectKeys =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
            KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0
        };

        [SerializeField] private Transform hero;
        [SerializeField] private CoverProbeProjectile projectilePrefab;
        [SerializeField] private MeleeSwingVisual swingPrefab;
        /// <summary>Index = CombatWeaponId. Entry 0 (Hand) may stay empty; weapon sprites are read from these prefabs.</summary>
        [SerializeField] private GameObject[] weaponPrefabs = new GameObject[CombatWeaponIds.Count];

        private IBalanceConfigProvider _balance;
        private IPoolService _pool;
        private IHudStateWriter _hud;
        private FakeCombatBackend _server;
        private WeaponController _controller;
        private PooledPrefabFactory<CoverProbeProjectile> _projectiles;
        private PooledPrefabFactory<MeleeSwingVisual> _swings;
        private readonly Sprite[] _weaponSprites = new Sprite[CombatWeaponIds.Count];
        private readonly float[] _pelletAngles = new float[16];
        private System.Action<CombatEvent> _onChanged;
        private float _aimDegrees;
        private float _serverTickAccumulator;

        [Inject]
        public void Construct(IBalanceConfigProvider balance, IPoolService pool, IHudStateWriter hud, FakeCombatBackend server)
        {
            _balance = balance;
            _pool = pool;
            _hud = hud;
            _server = server;
        }

        private void Start()
        {
            if (hero == null || projectilePrefab == null || swingPrefab == null || _pool == null || _server == null || _balance == null)
            {
                enabled = false;
                return;
            }
            _projectiles = new PooledPrefabFactory<CoverProbeProjectile>(_pool, projectilePrefab, 16);
            _swings = new PooledPrefabFactory<MeleeSwingVisual>(_pool, swingPrefab, 4);
            for (var i = 0; i < weaponPrefabs.Length && i < _weaponSprites.Length; i++)
            {
                if (weaponPrefabs[i] == null) continue;
                var renderer = weaponPrefabs[i].GetComponentInChildren<SpriteRenderer>(true);
                if (renderer != null) _weaponSprites[i] = renderer.sprite;
            }

            var peer = _server.AddPeer(LocalNetId);
            _controller = new WeaponController(_balance, peer, _hud, LocalNetId);
            peer.AttackResult += _controller.OnAttackResult;
            peer.ReloadResult += _controller.OnReloadResult;
            _onChanged = OnCombatEvent;
            _controller.Changed += _onChanged;
            _controller.Select(CombatWeaponId.Hand);
        }

        private void Update()
        {
            if (_controller == null) return;

            // Stand-in for the server: answer requests from the previous frame, then advance ticks at 30 Hz.
            _server.Flush();
            _serverTickAccumulator += Time.deltaTime;
            var step = 1f / CombatShotMath.TickRate;
            while (_serverTickAccumulator >= step)
            {
                _serverTickAccumulator -= step;
                _server.AdvanceTicks(1);
            }

            for (var i = 0; i < SelectKeys.Length; i++)
            {
                if (Input.GetKeyDown(SelectKeys[i])) _controller.Select((CombatWeaponId)i);
            }
            if (Input.GetKeyDown(KeyCode.R)) _controller.TryReload();

            if (Input.GetMouseButton(0))
            {
                if (HeroAim.TryGetTargetDegrees(Camera.main, hero.position, Input.mousePosition, out var degrees)) _aimDegrees = degrees;
                _controller.TryAttack(_aimDegrees);
            }
            _controller.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (_controller == null) return;
            _controller.Changed -= _onChanged;
        }

        private void OnCombatEvent(CombatEvent combatEvent)
        {
            if (combatEvent.Kind != CombatEventKind.AttackPredicted) return;
            SpawnAttackVisual(combatEvent.Weapon, combatEvent.AimDegrees);
        }

        private void SpawnAttackVisual(CombatWeaponId weapon, float aimDegrees)
        {
            var slot = _controller.Slot(weapon);
            if (slot.IsRanged)
            {
                var shot = new ConfiguredShot(_balance, CombatWeaponIds.ToJsonId(weapon));
                var count = CombatShotMath.PelletAngles(aimDegrees, slot.Pellets, slot.SpreadDegrees, _pelletAngles);
                var pose = new SpawnPose(hero.position, Quaternion.identity);
                for (var i = 0; i < count; i++)
                {
                    var radians = _pelletAngles[i] * Mathf.Deg2Rad;
                    var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                    var projectile = _projectiles.Create(in pose);
                    projectile.Launch(in shot, direction);
                }
                return;
            }

            var swing = _swings.Create(SpawnPose.At(hero.position));
            var duration = Mathf.Max(0.1f, slot.CadenceSeconds * 0.6f);
            swing.Play(_weaponSprites[(int)weapon], hero, aimDegrees, slot.ArcDegrees, slot.Range, duration);
            _pool.Despawn(swing, duration + 0.05f);
        }
    }
}
