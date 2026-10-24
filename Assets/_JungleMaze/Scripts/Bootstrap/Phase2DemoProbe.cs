using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Match;
using JungleMaze.App.Ports;
using JungleMaze.Core.DI;
using JungleMaze.Domain.Player;
using JungleMaze.Presentation.Player;
using JungleMaze.Presentation.UI;
using UnityEngine;

namespace JungleMaze.Composition
{
    /// <summary>Opt-in player-build acceptance: -phase2Smoke absolute-output.json. Never runs during normal play.</summary>
    public sealed class Phase2DemoProbe : MonoBehaviour
    {
        [Serializable] private sealed class Evidence
        {
            public bool passed, joined, twoViews, moved, otherMoved, zone, hpDamage, result, replay, guestReady, historyLoaded, identitySent;
            public int snapshots, rounds, ignoredPackets;
            public float seconds;
            public string reason;
        }
        private readonly Evidence _evidence = new Evidence();
        private readonly HashSet<Guid> _rounds = new HashSet<Guid>();
        private MatchSession _match;
        private ConnectionMenuPresenter _menu;
        private IPlayerHistory _history;
        private MatchDemoView _view;
        private string _output;
        private float _started, _firstInGame;
        private Vector2? _firstPosition;
        private Vector2? _firstOtherPosition;
        private Guid _replayed;
        private bool _finished, _captured, _resultCaptured;
        private bool _checkingHistory;

        [Inject] public void Construct(MatchSession match, ConnectionMenuPresenter menu, IPlayerHistory history)
        { _match = match; _menu = menu; _history = history; }

        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i + 1 < args.Length; i++) if (args[i] == "-phase2Smoke") _output = args[i + 1];
            if (string.IsNullOrEmpty(_output)) { enabled = false; yield break; }
            Application.runInBackground = true; Application.targetFrameRate = 60;
            _started = Time.realtimeSinceStartup;
            yield return new WaitForSecondsRealtime(1);
            _view = GetComponent<MatchDemoView>(); _view.UseExternalIntent = true;
            _menu.Join();
        }

        private void LateUpdate()
        {
            if (string.IsNullOrEmpty(_output) || _finished || _match == null) return;
            _evidence.seconds = Time.realtimeSinceStartup - _started;
            _evidence.joined |= _match.Connected;
            _evidence.guestReady |= _history.IsReady;
            _evidence.identitySent |= _match.IdentityTicketSent;
            _evidence.snapshots = _match.Snapshots; _evidence.ignoredPackets = _match.IgnoredPackets;
            if (_match.Phase == MatchPhase.InGame && _match.Snapshots > 0)
            {
                _rounds.Add(_match.MatchId); _evidence.rounds = _rounds.Count;
                if (_firstInGame == 0) _firstInGame = Time.realtimeSinceStartup;
                var moving = Time.realtimeSinceStartup - _firstInGame < 3;
                _match.SetIntent(new PlayerIntent { MoveX = moving ? -.25f : 0, MoveY = moving ? .2f : 0, AimDegrees = 45 });
                if (_match.Players.TryGet(_match.OwnNetId, out var own) && own.HasState)
                {
                    var position = new Vector2(own.Current.X, own.Current.Y);
                    if (!_firstPosition.HasValue) _firstPosition = position;
                    _evidence.moved |= Vector2.Distance(position, _firstPosition.Value) > .5f;
                    _evidence.hpDamage |= own.Current.Hp < 100;
                }
                var spawner = FindAnyObjectByType<RemotePlayerSpawner>(); var visible = 0;
                if (spawner != null) foreach (var id in _match.Players.ActiveIds)
                {
                    var playerView = spawner.GetView(id);
                    if (playerView == null || Camera.main == null) continue;
                    var point = Camera.main.WorldToViewportPoint(playerView.transform.position);
                    if (point.z > 0 && point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1) visible++;
                    if (id != _match.OwnNetId && _match.Players.TryGet(id, out var other) && other.HasState)
                    {
                        var p = new Vector2(other.Current.X, other.Current.Y);
                        if (!_firstOtherPosition.HasValue) _firstOtherPosition = p;
                        _evidence.otherMoved |= Vector2.Distance(p, _firstOtherPosition.Value) > .5f;
                    }
                }
                _evidence.twoViews |= visible >= 2;
                _evidence.zone |= _match.HasZone;
                if (!_captured && _evidence.twoViews && _evidence.moved)
                { ScreenCapture.CaptureScreenshot(_output + ".png"); _captured = true; }
            }
            if (_match.HasResult)
            {
                _evidence.result |= _match.Result.ParticipantCount >= 2;
                if (_history.IsReady && !_checkingHistory) { _checkingHistory = true; CheckHistory(); }
                if (!_resultCaptured) { ScreenCapture.CaptureScreenshot(_output + ".result.png"); _resultCaptured = true; }
                if (_match.Phase == MatchPhase.Ending && _replayed != _match.MatchId)
                { _replayed = _match.MatchId; _match.Replay(); }
            }
            _evidence.replay = _rounds.Count >= 2;
            if (_evidence.replay && _evidence.twoViews && _evidence.moved && _evidence.otherMoved && _evidence.zone && _evidence.hpDamage && _evidence.result && (!_evidence.identitySent || _evidence.historyLoaded))
                Finish(true, "Two Unity clients completed authoritative match and entered next round.");
            else if (_evidence.seconds > 100) Finish(false, "Demo acceptance deadline; inspect player log and server readiness.");
        }

        private async void CheckHistory()
        {
            for (var attempt = 0; attempt < 5 && !_finished; attempt++)
            {
                await Task.Delay(300);
                await _history.RefreshHistoryAsync();
                if (_history.Rows.Count > 0) { _evidence.historyLoaded = true; return; }
            }
        }

        private void Finish(bool passed, string reason)
        {
            _finished = true; _evidence.passed = passed; _evidence.reason = reason;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_output)));
            File.WriteAllText(_output, JsonUtility.ToJson(_evidence, true));
            Debug.Log("[Phase2Demo] " + reason);
            _match.Leave(); _menu.Disconnect(); Application.Quit(passed ? 0 : 1);
        }
    }
}
