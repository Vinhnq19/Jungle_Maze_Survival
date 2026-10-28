using System;
using System.Globalization;
using JungleMaze.App.Config;
using JungleMaze.App.Flow;
using JungleMaze.App.Match;
using JungleMaze.App.Network;
using JungleMaze.App.Ports;
using JungleMaze.Core.DI;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Network;

namespace JungleMaze.Presentation.UI
{
    /// <summary>
    /// Presenter (MVP) của menu kết nối: giữ dữ liệu form, gọi use case, dịch event sang text cho view.
    /// Không phụ thuộc UnityEngine nên view IMGUI có thể thay bằng uGUI/UI Toolkit mà không sửa logic.
    /// </summary>
    public sealed class ConnectionMenuPresenter : IInitializable, IDisposable
    {
        private readonly Action _beforeDisconnect;
        private readonly IConfigProvider _config;
        private readonly ConnectToServerUseCase _connect;
        private readonly INetworkClient _network;
        private readonly GameFlow _flow;
        private readonly IEventBus _bus;
        private readonly OfflineMatchSession _offlineSession;
        private readonly Action<AppScreenChanged> _onScreen;
        private readonly Action<ConnectionError> _onError;

        public ConnectionMenuPresenter(IConfigProvider config, ConnectToServerUseCase connect, INetworkClient network,
            GameFlow flow, IEventBus bus, Action beforeDisconnect = null, OfflineMatchSession offlineSession = null)
        {
            _beforeDisconnect = beforeDisconnect;
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _connect = connect ?? throw new ArgumentNullException(nameof(connect));
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _offlineSession = offlineSession;
            _onScreen = e => Status = e.Message;
            _onError = e => Status = e.Message;
            RoomCode = config.CommandLineRoomCode ?? "";
            DisplayName = config.CommandLineDisplayName ?? "Player";
            LoadFromConfig();
        }

        // Dữ liệu form (view đọc/ghi trực tiếp).
        public string Host, Port, ApiBaseUrl, RoomCode, DisplayName, Username = "", Password = "";
        public ConnectionMode Mode;
        public bool AdvancedExpanded { get; set; }
        public string SelectedOnlineTarget { get; set; } = "AWS";

        public AppScreen Screen => _flow.Screen;
        public string Status { get; private set; } = "";
        public string ConfigSource => _config.Current?.Source ?? "";
        public string AuthModeText => _config.Current?.AuthMode.ToString() ?? "";
        public string TransportText => _config.Current?.Transport ?? "";
        public bool RequiresLogin => _config.Current != null && _config.Current.AuthMode == JungleBackend.Protocol.JoinAuthMode.Jwt;
        public bool IsBusy => _connect.IsLoggingIn || _network.State == NetworkState.Connecting || _network.State == NetworkState.Authenticating;
        public bool IsOffline => _offlineSession != null && _offlineSession.IsActive;
        public string PlayerId => _network.PlayerId ?? "";
        public string Endpoint => _network.Endpoint;
        public string[] Profiles
        {
            get
            {
                var result = new string[_config.Profiles.Count];
                for (var i = 0; i < result.Length; i++) result[i] = _config.Profiles[i];
                return result;
            }
        }

        public void Initialize()
        {
            _bus.Subscribe(_onScreen);
            _bus.Subscribe(_onError);
            Status = _flow.Message;
        }

        public void Dispose()
        {
            _bus.Unsubscribe(_onScreen);
            _bus.Unsubscribe(_onError);
        }

        public void SelectProfile(string profile)
        {
            var error = _config.SelectProfile(profile);
            if (error != null) { Status = error; return; }
            LoadFromConfig();
            Status = $"Đã chọn profile {profile}.";
        }

        public void SelectTarget(string target)
        {
            var current = _config.Current;
            if (current == null) return;
            var port = int.TryParse(Port, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) ? p : 7777;

            if (string.Equals(target, "Local", StringComparison.OrdinalIgnoreCase))
            {
                Mode = ConnectionMode.Local;
                Status = "Chế độ Local: Solo Offline hoặc Loopback.";
            }
            else if (string.Equals(target, "LAN", StringComparison.OrdinalIgnoreCase))
            {
                Mode = ConnectionMode.LAN;
                var host = (string.IsNullOrWhiteSpace(Host) || Host == "127.0.0.1" || Host == "localhost" || Host == "junglemaze.duckdns.org") ? "192.168.1.10" : Host;
                var updated = current.WithEndpoint(host, port, ConnectionMode.LAN, "menu")
                    .WithApiBaseUrl(null, "menu").WithTransport("Udp", null);
                var error = _config.SaveUserConfig(updated);
                if (error != null) { Status = error; return; }
                LoadFromConfig();
                Status = "Chế độ LAN: UDP.";
            }
            else if (string.Equals(target, "Radmin", StringComparison.OrdinalIgnoreCase))
            {
                SelectedOnlineTarget = "Radmin";
                Mode = ConnectionMode.Online;
                var host = (string.IsNullOrWhiteSpace(Host) || Host == "127.0.0.1" || Host == "junglemaze.duckdns.org") ? "100.80.1.20" : Host;
                var updated = current.WithEndpoint(host, port, ConnectionMode.Online, "menu")
                    .WithApiBaseUrl(null, "menu").WithTransport("Udp", null);
                var error = _config.SaveUserConfig(updated);
                if (error != null) { Status = error; return; }
                LoadFromConfig();
                Status = "Chế độ Online (Radmin/VPN): UDP.";
            }
            else if (string.Equals(target, "AWS", StringComparison.OrdinalIgnoreCase))
            {
                SelectedOnlineTarget = "AWS";
                Mode = ConnectionMode.Online;
                Host = "junglemaze.duckdns.org";
                ApiBaseUrl = "https://junglemaze.duckdns.org";
                var updated = current.WithEndpoint("junglemaze.duckdns.org", port, ConnectionMode.Online, "menu")
                    .WithApiBaseUrl("https://junglemaze.duckdns.org", "menu")
                    .WithTransport("Auto", "wss://junglemaze.duckdns.org/game");
                var error = _config.SaveUserConfig(updated);
                if (error != null) { Status = error; return; }
                LoadFromConfig();
                Status = "Chế độ Online (AWS): junglemaze.duckdns.org (Auto UDP->WSS).";
            }
        }

        public void StartOffline()
        {
            if (_offlineSession == null)
            {
                Status = "Không có offline session.";
                return;
            }
            if (_network.State != NetworkState.Disconnected) _network.Disconnect();
            try { _offlineSession.StartRound(); _flow.StartOfflineMatch(); }
            catch (InvalidOperationException e) { _offlineSession.Leave(); Status = e.Message; }
        }

        /// <summary>Lưu host/port/mode người dùng sửa vào persistentDataPath/client.json (lần sau tự dùng).</summary>
        public bool SaveEndpoint()
        {
            if (!TryBuildConfig(out var config)) return false;
            var error = _config.SaveUserConfig(config);
            Status = error ?? "Đã lưu endpoint vào client.json.";
            return error == null;
        }

        public void Join()
        {
            if (!TryBuildConfig(out var config)) return;
            // Endpoint sửa trên menu được lưu vào client.json trước khi vào phòng: lần mở sau dùng lại, không cần build.
            if (!ReferenceEquals(config, _config.Current))
            {
                var error = _config.SaveUserConfig(config);
                if (error != null) { Status = error; return; }
            }
            var result = _connect.Execute(new ConnectForm
            {
                RoomCode = RoomCode, DisplayName = DisplayName, Username = Username, Password = Password
            });
            Password = ""; // không giữ password sau khi đã gửi đi
            if (result == null && RequiresLogin) Status = "Đang đăng nhập...";
        }

        public void Disconnect()
        {
            if (IsOffline)
            {
                _offlineSession.Leave();
                _flow.ReturnToMenu();
                return;
            }
            _beforeDisconnect?.Invoke();
            _network.Disconnect();
        }

        private void LoadFromConfig()
        {
            var config = _config.Current;
            if (config == null) return;
            Host = config.GameServerHost;
            Port = config.GameServerPort.ToString(CultureInfo.InvariantCulture);
            ApiBaseUrl = config.ApiBaseUrl ?? "";
            Mode = config.Mode;
        }

        private bool TryBuildConfig(out ClientConfig config)
        {
            config = _config.Current;
            if (config == null) { Status = "Chưa có config."; return false; }
            if (!int.TryParse(Port, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port))
            {
                Status = "Port phải là số nguyên.";
                return false;
            }
            var host = Host?.Trim();
            var api = string.IsNullOrWhiteSpace(ApiBaseUrl) ? null : ApiBaseUrl.Trim();
            if (host == config.GameServerHost && port == config.GameServerPort && Mode == config.Mode && api == config.ApiBaseUrl)
                return true;
            config = config.WithEndpoint(host, port, Mode, "menu").WithApiBaseUrl(api, "menu");
            var invalid = config.Validate();
            if (invalid != null) { Status = invalid; return false; }
            return true;
        }
    }
}
