using JungleMaze.Domain.Network;

namespace JungleMaze.App.Lobby
{
    public enum ApiOp : byte { Login, Register, ListRooms, CreateRoom, DeleteRoom, History, Leaderboard, Health }

    /// <summary>
    /// Vietnamese line for every API answer (AC12, AC14–AC16, AC35, AC37, AC41). Never the raw body, a status
    /// code alone, a token or a password.
    /// </summary>
    public static class ApiMessages
    {
        public const string NoApi = "Không có API: chỉ chơi bằng mã phòng";
        public const string ApiReady = "API sẵn sàng";
        public const string ApiDown = "API tạm ngừng";
        public const string AccountUnreachable = "Không kết nối được máy chủ tài khoản";
        public const string SessionExpired = "Phiên đăng nhập đã hết hạn, hãy đăng nhập lại";
        public const string RoomsUnavailable = "Máy chủ phòng chưa sẵn sàng";
        public const string NoRooms = "Bạn chưa có phòng";
        public const string HistoryEmpty = "Chưa có trận nào";
        public const string HistoryDown = "Dịch vụ lịch sử tạm ngừng";
        public const string HistoryUnavailableLine = "Lịch sử không khả dụng";
        public const string GuestUnsaved = "Khách: lịch sử chưa được lưu";
        public const string LeaderboardEmpty = "Kết quả trận chưa được lưu";

        public static string For<T>(ApiOp op, in ApiResult<T> r)
        {
            if (r.Ok) return "";
            var s = r.HttpStatus;
            var code = r.ErrorCode;
            switch (op)
            {
                case ApiOp.Login:
                    if (s == 401) return "Sai tên hoặc mật khẩu";
                    if (s == 429) return "Thử quá nhiều lần, thử lại sau";
                    return s == 0 || s >= 500 ? AccountUnreachable : "Đăng nhập không thành công";
                case ApiOp.Register:
                    if (s == 400 || s == 409) return "Tên đã được dùng hoặc không hợp lệ";
                    return s == 0 || s >= 500 ? AccountUnreachable : "Đăng ký không thành công";
                case ApiOp.ListRooms:
                    if (s == 401) return SessionExpired;
                    return "Danh sách phòng tạm ngừng";
                case ApiOp.CreateRoom:
                    if (s == 401) return SessionExpired;
                    if (s == 409 && code == "UserRoomLimitReached") return "Mỗi tài khoản tối đa " + Domain.Lobby.LobbyLimits.RoomsPerUser + " phòng";
                    if (s == 409 && code == "RoomLimitReached") return "Máy chủ đã đủ " + Domain.Lobby.LobbyLimits.Rooms + " phòng";
                    if (s == 429) return "Tạo quá nhanh, thử lại sau 1 phút";
                    if (s == 400) return "Tên hoặc sức chứa không hợp lệ";
                    return s == 503 ? RoomsUnavailable : "Không tạo được phòng, thử lại";
                case ApiOp.DeleteRoom:
                    if (s == 401) return SessionExpired;
                    if (s == 403) return "Bạn không phải chủ phòng";
                    if (s == 404) return "Phòng không còn";
                    if (s == 409 && code == "RoomNotEmpty") return "Phòng đang có người";
                    if (s == 409 && code == "DefaultRoomProtected") return "Không xóa được phòng mặc định";
                    return s == 503 ? RoomsUnavailable : "Không xóa được phòng, thử lại";
                case ApiOp.History:
                    return HistoryDown;
                case ApiOp.Leaderboard:
                    return s == 404 ? LeaderboardEmpty : "Bảng xếp hạng online tạm ngừng";
                default:
                    return ApiDown;
            }
        }
    }

    /// <summary>Join reject reason → one line (AC7, AC8). Unknown reasons show their name, never a stack trace.</summary>
    public static class JoinRejectText
    {
        public const string WrongCode = "Mã phòng không đúng hoặc phòng không còn";
        public const string Unreachable = "Không kết nối được tới máy chủ";

        public static string For(DisconnectReason reason)
        {
            switch (reason)
            {
                case DisconnectReason.None:
                case DisconnectReason.UserRequested: return "";
                case DisconnectReason.InvalidRoomCode: return WrongCode;
                case DisconnectReason.RoomFull: return "Phòng đã đủ " + Domain.Lobby.LobbyLimits.PlayersPerRoom + " người";
                case DisconnectReason.ServerFull: return "Máy chủ đã đầy, thử lại sau";
                case DisconnectReason.AuthenticationRequired:
                case DisconnectReason.AuthModeMismatch: return "Máy chủ yêu cầu đăng nhập";
                case DisconnectReason.InvalidConfig: return "Cấu hình máy chủ không hợp lệ: kiểm tra host/port ở Menu chính";
                case DisconnectReason.LoginFailed: return "Máy chủ yêu cầu đăng nhập: đăng nhập không thành công";
                case DisconnectReason.InvalidDisplayName: return "Tên hiển thị không hợp lệ (1–32 ký tự)";
                case DisconnectReason.ConnectTimeout:
                case DisconnectReason.JoinTimeout:
                case DisconnectReason.SocketError: return Unreachable;
                default: return "Không vào được phòng (mã lỗi: " + reason + ")";
            }
        }
    }
}
