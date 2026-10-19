namespace JungleMaze.Domain.Network
{
    /// <summary>Lý do thất bại/ngắt kết nối. Nhóm đầu khớp chuỗi reason của GameServer.</summary>
    public enum DisconnectReason : byte
    {
        None = 0,

        // Server từ chối (JoinRejected / dữ liệu disconnect).
        MalformedRequest,
        PayloadTooLarge,
        UnsupportedVersion,
        AuthModeMismatch,
        InvalidRoomCode,
        InvalidDisplayName,
        RoomFull,
        ServerFull,
        AuthenticationRequired,
        InvalidToken,
        AuthTimeout,
        DuplicateSession,

        // Phía client.
        UserRequested,
        ConnectTimeout,
        JoinTimeout,
        ConnectionLost,
        ServerClosed,
        SocketError,
        ProtocolError,
        InvalidConfig,
        LoginFailed,
        Unknown
    }

    public static class DisconnectReasons
    {
        /// <summary>Chuỗi reason từ server → enum; chuỗi lạ thành Unknown chứ không ném lỗi.</summary>
        public static DisconnectReason FromServer(string reason)
        {
            switch (reason)
            {
                case "MalformedRequest": return DisconnectReason.MalformedRequest;
                case "PayloadTooLarge": return DisconnectReason.PayloadTooLarge;
                case "UnsupportedVersion": return DisconnectReason.UnsupportedVersion;
                case "AuthModeMismatch": return DisconnectReason.AuthModeMismatch;
                case "InvalidRoomCode": return DisconnectReason.InvalidRoomCode;
                case "InvalidDisplayName": return DisconnectReason.InvalidDisplayName;
                case "RoomFull": return DisconnectReason.RoomFull;
                case "ServerFull": return DisconnectReason.ServerFull;
                case "AuthenticationRequired": return DisconnectReason.AuthenticationRequired;
                case "InvalidToken": return DisconnectReason.InvalidToken;
                case "AuthTimeout": return DisconnectReason.AuthTimeout;
                case "DuplicateSession": return DisconnectReason.DuplicateSession;
                default: return DisconnectReason.Unknown;
            }
        }

        /// <summary>Thông báo tiếng Việt cho UI; không chứa dữ liệu nhạy cảm (mã phòng, token).</summary>
        public static string Describe(DisconnectReason reason)
        {
            switch (reason)
            {
                case DisconnectReason.None: return "";
                case DisconnectReason.UnsupportedVersion: return "Phiên bản client không khớp server.";
                case DisconnectReason.AuthModeMismatch: return "Chế độ xác thực không khớp server (RoomCode/Jwt).";
                case DisconnectReason.InvalidRoomCode: return "Mã phòng không đúng.";
                case DisconnectReason.InvalidDisplayName: return "Tên hiển thị không hợp lệ (1–32 ký tự).";
                case DisconnectReason.RoomFull: return "Phòng đã đủ người.";
                case DisconnectReason.ServerFull: return "Server đã đủ người.";
                case DisconnectReason.AuthenticationRequired: return "Server yêu cầu đăng nhập.";
                case DisconnectReason.InvalidToken: return "Phiên đăng nhập không hợp lệ hoặc đã hết hạn.";
                case DisconnectReason.AuthTimeout: return "Xác thực quá thời gian.";
                case DisconnectReason.DuplicateSession: return "Tài khoản đang có phiên khác trong phòng.";
                case DisconnectReason.UserRequested: return "Đã ngắt kết nối.";
                case DisconnectReason.ConnectTimeout: return "Không kết nối được server (hết thời gian). Kiểm tra IP/port và server.";
                case DisconnectReason.JoinTimeout: return "Đã nối server nhưng không vào được phòng (hết thời gian).";
                case DisconnectReason.ConnectionLost: return "Mất kết nối tới server.";
                case DisconnectReason.ServerClosed: return "Server đã đóng kết nối.";
                case DisconnectReason.SocketError: return "Lỗi mạng (socket).";
                case DisconnectReason.ProtocolError: return "Server gửi dữ liệu không đúng giao thức.";
                case DisconnectReason.InvalidConfig: return "Cấu hình kết nối không hợp lệ.";
                case DisconnectReason.LoginFailed: return "Đăng nhập thất bại.";
                case DisconnectReason.MalformedRequest:
                case DisconnectReason.PayloadTooLarge: return "Yêu cầu vào phòng không hợp lệ.";
                default: return "Lỗi không xác định.";
            }
        }
    }
}
