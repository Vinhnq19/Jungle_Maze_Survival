using System.Globalization;
using JungleMaze.App.Match;
using JungleMaze.App.Player;
using JungleMaze.App.Ports;
using JungleMaze.Core.DI;
using UnityEngine;

namespace JungleMaze.Presentation.UI
{
    /// <summary>Overlay số đo mạng (F3), chỉ có trong Editor/Development build. Dùng để ghi RTT thực tế khi nghiệm thu.</summary>
    public sealed class NetworkStatsOverlay : MonoBehaviour
    {
        private const float RefreshSeconds = 0.5f;
        private INetworkClient _network;
        private MatchSession _match;
        private RemotePlayerInterpolator _remote;
        private bool _visible;
        private float _nextRefresh, _fps;
        private int _frames;
        private string _text = "";

        [Inject]
        public void Construct(INetworkClient network, MatchSession match, RemotePlayerInterpolator remote)
        {
            _network = network;
            _match = match;
            _remote = remote;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3)) _visible = !_visible;
            _frames++;
            if (!_visible || _network == null || Time.unscaledTime < _nextRefresh) return;
            _fps = _frames / RefreshSeconds;
            _frames = 0;
            _nextRefresh = Time.unscaledTime + RefreshSeconds;
            // Chỉ format chuỗi 2 lần/giây khi overlay bật, không ảnh hưởng hot path.
            var s = _network.Stats;
            _text = string.Format(CultureInfo.InvariantCulture,
                "State: {0} ({1})\nEndpoint: {2}\nRTT ping/pong: last {3:F1} ms · avg {4:F1} · p95 {5:F1}\nRTT LiteNetLib: {6} ms\n" +
                "Ping sent {7} · pong {8} · lost {9}\nPackets out {10} / in {11} · bytes out {12} / in {13}\nInvalid packets: {14} · FPS {15:F0}",
                _network.State, _network.LastReason, _network.Endpoint, s.LastRttMs, s.AverageRttMs, s.P95RttMs, s.PeerRttMs,
                s.PingsSent, s.PongsReceived, s.PongsLost, s.PacketsSent, s.PacketsReceived, s.BytesSent, s.BytesReceived,
                s.InvalidPackets, _fps);
            _text += string.Format(CultureInfo.InvariantCulture,
                "\nTransport: {0} · fallback: {1} · p50 {2:F1}/p99 {3:F1}ms",
                s.Transport, s.FallbackReason, s.P50RttMs, s.P99RttMs);
            var p = _match.Prediction.Stats;
            var r = _remote.Stats;
            _text += string.Format(CultureInfo.InvariantCulture,
                "\nMOV-02 ack {0:F0} ms · pending {1} · sửa {2} · snap {3} · lệch avg {4:F3}/max {5:F3}" +
                "\nMOV-03 jitter {6:F1} ms · trễ {7:F0} ms · ngoại suy {8}/{9} · trùng {10} · cũ {11}",
                p.AverageAckRttMs, p.Pending, p.Corrections, p.Snaps, p.AverageErrorMeters, p.MaxErrorMeters,
                r.JitterMs, r.RenderDelayMs, r.ExtrapolatedSamples, r.Samples, r.Duplicates, r.TooOld);
        }

        private void OnGUI()
        {
            if (!_visible) return;
            GUI.Box(new Rect(Screen.width - 490, 10, 480, 190), GUIContent.none);
            GUI.Label(new Rect(Screen.width - 480, 15, 460, 180), _text);
        }
#endif
    }
}
