using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Combat;
using JungleMaze.App.Combat.Mocks;
using JungleMaze.App.Match;
using JungleMaze.App.MatchUi;
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
    /// Weapon input and pooled visuals. Online play uses the authoritative combat session;
    /// the in-process backend is retained only for disconnected local previews.
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
        private MatchSession _match;
        private OnlineCombatSession _online;
        private IMatchMenuState _menu;
        private IPlayerPoseSource _poses;
        private WeaponController _controller;
        private PooledPrefabFactory<CoverProbeProjectile> _projectiles;
        private PooledPrefabFactory<MeleeSwingVisual> _swings;
        private readonly Sprite[] _weaponSprites = new Sprite[CombatWeaponIds.Count];
        private readonly float[] _pelletAngles = new float[16];
        private System.Action<CombatEvent> _onChanged;
        private float _aimDegrees;
        private float _serverTickAccumulator;

        [Inject]
        public void Construct(IBalanceConfigProvider balance, IPoolService pool, IHudStateWriter hud,
            MatchSession match, OnlineCombatSession online, IMatchMenuState menu, IPlayerPoseSource poses)
        {
            _balance = balance;
            _pool = pool;
            _hud = hud;
            _match=match; _online=online; _menu=menu; _poses=poses;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Only development previews receive a fake backend; release online injection never requires it.</summary>
        [Inject] public void ConfigurePreview(FakeCombatBackend server) => _server=server;
#endif

        private void Start()
        {
            if (hero == null || projectilePrefab == null || swingPrefab == null || _pool == null || _balance == null)
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

            if (!_match.Connected && _server!=null) CreateOfflineController();
            _online.Changed+=OnCombatEvent;
        }

        private void CreateOfflineController()
        {
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
            if (_online==null || _pool==null || _projectiles==null) return;
            if (_match.Connected)
            {
                if (!_online.CanAct || (_menu?.BlocksGameplayInput??false)) return;
                if (Input.GetKeyDown(KeyCode.R)) _online.TryReload();
                if (Input.GetMouseButton(0))
                {
                    if (HeroAim.TryGetTargetDegrees(Camera.main, hero.position, Input.mousePosition, out var onlineAim)) _aimDegrees=onlineAim;
                    _online.TryAttack(_aimDegrees);
                }
                return;
            }
            if (_server==null)return;
            if (_controller == null) CreateOfflineController();

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
            if (_controller != null) _controller.Changed -= _onChanged;
            if (_online!=null) _online.Changed-=OnCombatEvent;
        }

        private void OnCombatEvent(CombatEvent combatEvent)
        {
            if (combatEvent.Kind == CombatEventKind.AttackPredicted)
                SpawnAttackVisual(combatEvent.Weapon, combatEvent.AimDegrees, hero.position, hero);
            else if (combatEvent.Kind == CombatEventKind.RemoteAttackConfirmed && _poses!=null && _poses.TryGetPose(combatEvent.NetId,out var pose))
                SpawnAttackVisual(combatEvent.Weapon, pose.RotationDegrees, new Vector3(pose.X,pose.Y,0), null);
        }

        private void SpawnAttackVisual(CombatWeaponId weapon, float aimDegrees, Vector3 origin, Transform anchor)
        {
            var controller=_match.Connected?_online.Controller:_controller;
            if (controller==null) return;
            var slot = controller.Slot(weapon);
            if (slot.IsRanged)
            {
                var shot = new ConfiguredShot(_balance, CombatWeaponIds.ToJsonId(weapon));
                var count = CombatShotMath.PelletAngles(aimDegrees, slot.Pellets, slot.SpreadDegrees, _pelletAngles);
                var pose = new SpawnPose(origin, Quaternion.identity);
                for (var i = 0; i < count; i++)
                {
                    var radians = _pelletAngles[i] * Mathf.Deg2Rad;
                    var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                    var projectile = _projectiles.Create(in pose);
                    projectile.Launch(in shot, direction);
                }
                return;
            }

            var swing = _swings.Create(SpawnPose.At(origin));
            var duration = Mathf.Max(0.1f, slot.CadenceSeconds * 0.6f);
            swing.Play(_weaponSprites[(int)weapon], anchor, aimDegrees, slot.ArcDegrees, slot.Range, duration);
            _pool.Despawn(swing, duration + 0.05f);
        }
    }
}
