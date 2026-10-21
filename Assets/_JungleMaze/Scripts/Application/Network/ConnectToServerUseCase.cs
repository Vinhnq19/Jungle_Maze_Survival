using System;
using JungleBackend.Protocol;
using JungleMaze.App.Flow;
using JungleMaze.App.Ports;
using JungleMaze.Core.Events;

namespace JungleMaze.App.Network
{
    /// <summary>Dữ liệu người dùng nhập trên menu. Password chỉ dùng cho lần login này, không lưu.</summary>
    public struct ConnectForm
    {
        public string RoomCode;
        public string DisplayName;
        public string Username;
        public string Password;
    }

    /// <summary>
    /// Use case "vào phòng": kiểm config → (Jwt) login lấy token → INetworkClient.Connect.
    /// Lỗi trước transport publish ConnectionError; lỗi sau đó đi qua NetworkStateChanged.
    /// </summary>
    public sealed class ConnectToServerUseCase
    {
        private readonly IConfigProvider _config;
        private readonly INetworkClient _network;
        private readonly IAuthApi _auth;
        private readonly IEventBus _bus;

        public ConnectToServerUseCase(IConfigProvider config, INetworkClient network, IAuthApi auth, IEventBus bus)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        public bool IsLoggingIn { get; private set; }

        /// <summary>Trả lỗi ngay nếu dữ liệu sai; null nghĩa là đã bắt đầu (login hoặc connect).</summary>
        public string Execute(ConnectForm form)
        {
            if (IsLoggingIn) return Error("Đang đăng nhập, chờ một chút.");
            var config = _config.Current;
            var invalid = config?.Validate();
            if (config == null || invalid != null) return Error(invalid ?? "Chưa có config.");
            if (string.IsNullOrWhiteSpace(form.RoomCode)) return Error("Nhập mã phòng.");
            if (string.IsNullOrWhiteSpace(form.DisplayName)) return Error("Nhập tên hiển thị.");

            if (config.AuthMode != JoinAuthMode.Jwt)
            {
                _network.Connect(new ConnectRequest(config, form.RoomCode.Trim(), form.DisplayName.Trim()));
                return null;
            }

            IsLoggingIn = true;
            var room = form.RoomCode.Trim();
            var name = form.DisplayName.Trim();
            _auth.Login(config, form.Username, form.Password, result =>
            {
                IsLoggingIn = false;
                if (!result.Success) { Error(result.Error); return; }
                _network.Connect(new ConnectRequest(config, room, name, result.AccessToken));
            });
            return null;
        }

        private string Error(string message)
        {
            _bus.Publish(new ConnectionError(message));
            return message;
        }
    }
}
