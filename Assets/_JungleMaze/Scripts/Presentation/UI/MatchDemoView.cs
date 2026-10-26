using System;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Match;
using JungleMaze.App.Player;
using JungleMaze.App.Ports;
using JungleMaze.Core.DI;
using JungleMaze.Presentation.Controls;
using JungleMaze.Presentation.Player;
using JungleMaze.Presentation.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JungleMaze.Presentation.UI
{
    /// <summary>
    /// Small Phase-2 inspection UI and scene adapter. Gives MOV-02 prediction the verified map (same data the server
    /// simulates) and aims from the predicted Hero; full HUD/FOW remain separate tasks.
    /// </summary>
    public sealed class MatchDemoView : MonoBehaviour
    {
        /// <summary>Explicit acceptance runner only; ordinary play always reads WASD/mouse.</summary>
        public bool UseExternalIntent { get; set; }
        private MatchSession _session;
        private INetworkClient _network;
        private IMatchConnection _connection;
        private IPlayerHistory _history;
        private IClientRuntimeData _data;
        private IPlayerPoseSource _poses;
        private string _worldHash;
        private HeroController _hero;
        private RemotePlayerSpawner _spawner;
        private Camera _camera;
        private LineRenderer _currentRing, _nextRing;
        private Material _ringMaterial;
        private readonly Vector3[] _circle = new Vector3[96];
        private Guid _loadedAck;
        private string _dataError;
        private bool _historyVisible;
        private bool _busyHistory;
        private GUIStyle _label;

        [Inject]
        public void Construct(MatchSession session, INetworkClient network, IMatchConnection connection,
            IPlayerHistory history, IClientRuntimeData data, IPlayerPoseSource poses)
        { _session = session; _network = network; _connection = connection; _history = history; _data = data; _poses = poses; }

        private void Update()
        {
            if (_session == null) return;
            var inScene = SceneManager.GetActiveScene().name == "Game";
            if (!_session.Connected || !inScene) { HideRings(); return; }
            PrepareScene();
            if (_session.Phase != MatchPhase.Loading) _loadedAck = Guid.Empty;
            if (_session.Phase == MatchPhase.Loading && _loadedAck != _session.MatchId)
            {
                _dataError = _data.Verify(_session.PhaseState.ConfigVersion, _session.PhaseState.ConfigHash);
                if (_dataError == null) _dataError = VerifyGeometry();
                if (_dataError == null) { UsePredictionWorld(); _session.ConfirmLoaded(); _loadedAck = _session.MatchId; }
            }
            // Resume thẳng vào InGame (bỏ qua Loading) vẫn cần map cho prediction; chỉ kiểm, không gửi LoadAck.
            if (_session.Phase == MatchPhase.InGame && _worldHash != _session.PhaseState.ConfigHash && _dataError == null)
            {
                _dataError = _data.Verify(_session.PhaseState.ConfigVersion, _session.PhaseState.ConfigHash);
                if (_dataError == null) UsePredictionWorld();
            }
            var intent = new JungleMaze.Domain.Player.PlayerIntent();
            intent.MoveX = Input.GetAxisRaw("Horizontal"); intent.MoveY = Input.GetAxisRaw("Vertical");
            var origin = _hero != null ? _hero.transform.position : Vector3.zero;
            // Ngắm từ vị trí đang vẽ (đã dự đoán) để hướng chuột khớp với nhân vật người chơi nhìn thấy.
            if (_session.OwnNetId != 255 && _poses.TryGetPose(_session.OwnNetId, out var ownPose))
                origin = new Vector3(ownPose.X, ownPose.Y, 0);
            else if (_session.Players.TryGet(_session.OwnNetId, out var own) && own.HasState)
                origin = new Vector3(own.Current.X, own.Current.Y, 0);
            if (HeroAim.TryGetTargetDegrees(_camera, origin, Input.mousePosition, out var degrees)) intent.AimDegrees = degrees;
            if (!UseExternalIntent) _session.SetIntent(intent);
            if (_hero != null && _session.OwnNetId != 255)
            {
                var view = _spawner != null ? _spawner.GetView(_session.OwnNetId) : null;
                _hero.transform.position = view != null ? view.transform.position : origin;
            }
            if (_session.HasZone && _session.Phase == MatchPhase.InGame)
            {
                DrawRing(ref _currentRing, "Current server zone", _session.Zone.CurrentCenterX, _session.Zone.CurrentCenterY, _session.Zone.CurrentRadius, Color.red);
                DrawRing(ref _nextRing, "Next server zone", _session.Zone.TargetCenterX, _session.Zone.TargetCenterY, _session.Zone.TargetRadius, Color.yellow);
            }
            else HideRings();
        }

        private void PrepareScene()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera != null)
            {
                // Overview for the networking demo: safe spawns are at least six cells apart.
                // A narrow local camera can hide the other player even when snapshots are correct.
                if (_camera.TryGetComponent<HeroCameraFollow>(out var follow)) follow.enabled = false;
                _camera.orthographicSize = 28;
                _camera.transform.position = new Vector3(25, 25, -10);
            }
            if (_spawner == null) _spawner = FindAnyObjectByType<RemotePlayerSpawner>();
            if (_hero != null) return;
            _hero = FindAnyObjectByType<HeroController>();
            if (_hero == null) return;
            // Two controllers must never write the same transform: the local foundation is now only a camera proxy.
            _hero.enabled = false;
            if (_hero.TryGetComponent<Rigidbody2D>(out var body)) body.simulated = false;
            foreach (var renderer in _hero.GetComponentsInChildren<SpriteRenderer>()) renderer.enabled = false;
            foreach (var input in FindObjectsByType<PlayerInputReader>(FindObjectsSortMode.None)) input.enabled = false;
            foreach (var probe in FindObjectsByType<LocalWeaponProbe>(FindObjectsSortMode.None)) probe.enabled = false;
        }
        /// <summary>Map đã khớp hash server → mô phỏng movement client giống hệt server (MOV-02).</summary>
        private void UsePredictionWorld()
        {
            if (_worldHash == _session.PhaseState.ConfigHash && _session.Prediction.HasWorld) return;
            _session.Prediction.SetWorld(new MovementSimulation(_data.Bounds, _data.Covers, _data.PlayerRadius, _data.PlayerSpeed));
            _worldHash = _session.PhaseState.ConfigHash;
        }

        private string VerifyGeometry()
        {
            var grid = FindAnyObjectByType<MapGridView>();
            var map = _data.Bounds;
            if (grid == null || Mathf.Abs(grid.MapSizeCells - (map.MaxX - map.MinX)) > .001f ||
                Mathf.Abs(grid.MapSizeCells - (map.MaxY - map.MinY)) > .001f ||
                Vector3.Distance(grid.transform.position, new Vector3(map.MinX, map.MinY, 0)) > .001f ||
                Vector3.Distance(grid.transform.lossyScale, Vector3.one) > .001f)
                return "Kích thước/vị trí map trong scene khác server; khôi phục map demo.";
            var colliders = FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None);
            foreach (var cover in _data.Covers)
            {
                var found = false;
                foreach (var collider in colliders)
                {
                    if (collider.name != cover.Name || collider.gameObject.layer != LayerMask.NameToLayer("HardCover")) continue;
                    var bounds = collider.bounds;
                    if (Mathf.Abs(bounds.min.x - cover.MinX) > .002f || Mathf.Abs(bounds.max.x - cover.MaxX) > .002f ||
                        Mathf.Abs(bounds.min.y - cover.MinY) > .002f || Mathf.Abs(bounds.max.y - cover.MaxY) > .002f)
                        return "Vật cản trong scene khác dữ liệu server; đồng bộ lại map.";
                    found = true; break;
                }
                if (!found) return "Scene thiếu vật cản của map server; cập nhật client.";
            }
            return null;
        }
        private void DrawRing(ref LineRenderer ring, string title, float x, float y, float radius, Color color)
        {
            if (ring == null)
            {
                var obj = new GameObject(title); ring = obj.AddComponent<LineRenderer>();
                obj.transform.SetParent(transform, false);
                if (_ringMaterial == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (shader == null) shader = Shader.Find("Sprites/Default");
                    _ringMaterial = new Material(shader);
                }
                ring.sharedMaterial = _ringMaterial; ring.useWorldSpace = true; ring.loop = true;
                ring.widthMultiplier = .08f; ring.positionCount = _circle.Length; ring.sortingOrder = 20;
                ring.startColor = ring.endColor = color;
            }
            ring.enabled = radius > .001f;
            if (!ring.enabled) return;
            for (var i = 0; i < _circle.Length; i++)
            {
                var angle = i * Mathf.PI * 2 / _circle.Length;
                _circle[i] = new Vector3(x + Mathf.Cos(angle) * radius, y + Mathf.Sin(angle) * radius, -.1f);
            }
            ring.SetPositions(_circle);
        }
        private void HideRings()
        { if (_currentRing != null) _currentRing.enabled = false; if (_nextRing != null) _nextRing.enabled = false; }
        private void OnDestroy() { if (_ringMaterial != null) Destroy(_ringMaterial); }

        private void OnGUI()
        {
            if (_session == null) return;
            _label ??= new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false };
            if (!_session.Connected && !_session.CanResume) return;
            if (_session.Connected && _camera != null && _spawner != null)
                foreach (var id in _session.Players.ActiveIds)
                {
                    var view = _spawner.GetView(id);
                    if (view == null || !_session.Players.TryGet(id, out var player) || !player.HasState) continue;
                    var point = _camera.WorldToScreenPoint(view.transform.position);
                    if (point.z <= 0) continue;
                    var name = id == _session.OwnNetId ? "Bạn" : "Player " + id;
                    foreach (var row in _session.Roster) if (row.NetId == id) { name = (id == _session.OwnNetId ? "Bạn: " : "") + row.DisplayName; break; }
                    GUI.Label(new Rect(point.x - 50, Screen.height - point.y - 30, 150, 25), name + " · HP " + player.Current.Hp);
                }
            GUILayout.BeginArea(new Rect(_session.Connected ? 10 : 420, 52, 370, Mathf.Min(Screen.height - 62, 590)), GUI.skin.box);
            GUILayout.Label("Lastaway · Demo Phase 2", _label);
            if (!_session.Connected)
            {
                GUILayout.Label("Mất kết nối. Có thể khôi phục nếu phiên còn grace.", _label);
                if (GUILayout.Button("Khôi phục nhân vật")) _connection.Resume(_session.ResumeProof);
                GUILayout.EndArea(); return;
            }
            GUILayout.Label($"{_session.Phase} · {_session.PhaseState.PlayerCount}/{_session.PhaseState.MaxPlayers} người", _label);
            if (_session.Phase == MatchPhase.Lobby || _session.Phase == MatchPhase.Loading)
                GUILayout.Label($"Còn {_session.CountdownSeconds:F1}s · tối thiểu {_session.PhaseState.MinPlayers} người", _label);
            if (_session.Phase == MatchPhase.Waiting || _session.Phase == MatchPhase.Lobby || _session.Phase == MatchPhase.Loading)
                foreach (var row in _session.Roster) GUILayout.Label(row.DisplayName, _label);
            GUILayout.Label($"HP: {_session.OwnHp} · Tick server: {_session.ServerTick} · Snapshot: {_session.Snapshots}", _label);
            if (_dataError != null) GUILayout.Label(_dataError, _label);
            if (_session.HasZone) GUILayout.Label($"Bo {_session.Zone.PhaseIndex}: r={_session.Zone.CurrentRadius:F1}" + (_session.Zone.InWarning ? " · Sắp co" : ""), _label);
            var p = _session.Prediction.Stats;
            GUILayout.Label($"WASD di chuyển, chuột chọn hướng. Prediction: {(_session.Prediction.IsActive ? "bật" : "chờ")} · sửa {p.Corrections} · lệch max {p.MaxErrorMeters:F2}. Chưa có combat/FOW.", _label);
            if (_session.HasResult)
            {
                GUILayout.Label(_session.Result.Reason == MatchEndReason.Tie ? "HÒA — không có người thắng" : "KẾT QUẢ TRẬN", _label);
                foreach (var row in _session.Result.Participants ?? Array.Empty<MatchParticipantState>())
                    GUILayout.Label($"#{row.Rank} {row.DisplayName} · hạ gục {row.Kills} · {row.SurvivalTicks / 30f:F1}s", _label);
                if (_session.Phase == MatchPhase.Ending && GUILayout.Button("Sẵn sàng chơi lại")) _session.Replay();
            }
            GUILayout.Label(_history.Status, _label);
            GUI.enabled = !_busyHistory;
            if (GUILayout.Button("Xem / cập nhật lịch sử")) RefreshHistory();
            GUI.enabled = true;
            if (_historyVisible)
                foreach (var row in _history.Rows)
                    GUILayout.Label($"{row.EndedAt.ToLocalTime():dd/MM HH:mm} · #{row.Rank} · {row.SurvivalSeconds:F1}s · {row.Reason}", _label);
            GUILayout.EndArea();
        }
        private async void RefreshHistory()
        {
            _busyHistory = true;
            try { await _history.RefreshHistoryAsync(_connection.LastRequest.AccessToken); _historyVisible = true; }
            finally { _busyHistory = false; }
        }
    }
}
