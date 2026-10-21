#nullable enable
using System;
using LiteNetLib.Utils;

// Package com.junglemaze.protocol: GameServer/NetSmokeClient Compile Link, Unity nạp qua file: package.
// Giữ cú pháp C# 9 (Unity 6) — không file-scoped namespace, không API chỉ có trên net8.
namespace JungleBackend.Protocol
{
    public enum JoinAuthMode : byte { RoomCode = 0, Jwt = 1 }

    public sealed record JoinRequest(ushort Version, JoinAuthMode Mode, string RoomCode, string DisplayName, Guid AttemptId = default)
    {
        // Không để record.ToString() vô tình in mã phòng ra log.
        public override string ToString() => $"JoinRequest(version={Version}, mode={Mode})";
    }

    /// <summary>Hợp đồng wire dùng chung qua Compile Link; thay thế hoàn toàn key smoke SRV-01.</summary>
    public static class NetworkProtocol
    {
        public const ushort Version = 1;
        public const ushort LatestVersion = 2;
        public const byte Ping = 1, Pong = 2, Auth = 3, JoinAccepted = 4, JoinRejected = 5, AuthRequired = 6;
        public const int PacketBytes = 5, MaxConnectBytes = 256, MaxAuthBytes = 4096;
        public const int MaxNameChars = 32, MaxRoomChars = 64, MaxTokenChars = 2048;

        public static NetDataWriter WriteConnect(JoinAuthMode mode, string roomCode, string displayName,
            ushort version = Version, Guid attemptId = default)
        {
            if (string.IsNullOrWhiteSpace(roomCode) || roomCode.Length > MaxRoomChars
                || string.IsNullOrWhiteSpace(displayName) || displayName.Length > MaxNameChars)
                throw new ArgumentException("Mã phòng cần 1–64 ký tự, tên hiển thị cần 1–32 ký tự.");
            var writer = new NetDataWriter();
            if (version is not (Version or LatestVersion) || version == LatestVersion && attemptId == Guid.Empty)
                throw new ArgumentException("Protocol v2 cần mã lượt kết nối ngẫu nhiên, không rỗng.");
            writer.Put(version);
            writer.Put((byte)mode);
            writer.Put(roomCode);
            writer.Put(displayName);
            if (version == LatestVersion) writer.Put(attemptId.ToByteArray());
            // Chặn byte thực tế: Unicode có thể chiếm nhiều byte dù số ký tự vẫn đúng.
            if (writer.Length > MaxConnectBytes + (version == LatestVersion ? 16 : 0)) throw new ArgumentException("Connection request quá dài.");
            return writer;
        }

        public static bool TryReadConnect(NetDataReader reader, out JoinRequest? request, out string reason)
        {
            request = null;
            reason = reader.AvailableBytes > MaxConnectBytes + 16 ? "PayloadTooLarge" : "MalformedRequest";
            var length = reader.AvailableBytes;
            if (length > MaxConnectBytes + 16 || !reader.TryGetUShort(out var version)) return false;
            if (length > MaxConnectBytes + (version == LatestVersion ? 16 : 0)) { reason = "PayloadTooLarge"; return false; }
            if (!reader.TryGetByte(out var mode) || !reader.TryGetString(out var room)
                || !reader.TryGetString(out var name)) return false;
            if (version is not (Version or LatestVersion)) { reason = "UnsupportedVersion"; return false; }
            var attempt = Guid.Empty;
            if (version == LatestVersion)
            {
                if (reader.AvailableBytes != 16) return false;
                attempt = new Guid(reader.GetRemainingBytes());
                if (attempt == Guid.Empty) return false;
            }
            if (reader.AvailableBytes != 0) return false;
            if (mode > (byte)JoinAuthMode.Jwt) { reason = "AuthModeMismatch"; return false; }
            request = new JoinRequest(version, (JoinAuthMode)mode, room, name.Trim(), attempt);
            return true;
        }

        public static void Write(NetDataWriter writer, byte type, int sequence)
        {
            writer.Reset();
            writer.Put(type);
            writer.Put(sequence);
        }

        public static bool TryRead(NetDataReader reader, byte expectedType, out int sequence)
        {
            sequence = 0;
            // Ping/Pong phải đúng 5 byte và sequence > 0; không đọc packet thiếu/thừa.
            return reader.AvailableBytes == PacketBytes && reader.TryGetByte(out var type)
                && type == expectedType && reader.TryGetInt(out sequence) && sequence > 0;
        }

        public static NetDataWriter StringMessage(byte type, string value)
        {
            var writer = new NetDataWriter();
            writer.Put(type);
            writer.Put(value);
            return writer;
        }

        public static bool TryReadStringMessage(NetDataReader reader, byte expectedType, int maxBytes,
            int maxChars, out string value)
        {
            value = string.Empty;
            return reader.AvailableBytes <= maxBytes && reader.TryGetByte(out var type) && type == expectedType
                && reader.TryGetString(out value) && !string.IsNullOrWhiteSpace(value)
                && value.Length <= maxChars && reader.AvailableBytes == 0;
        }

        public static NetDataWriter WriteAuth(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length > MaxTokenChars)
                throw new ArgumentException("JWT thiếu hoặc vượt 2048 ký tự.");
            var writer = StringMessage(Auth, token);
            if (writer.Length > MaxAuthBytes) throw new ArgumentException("Auth packet vượt 4096 byte.");
            return writer;
        }
    }
}
