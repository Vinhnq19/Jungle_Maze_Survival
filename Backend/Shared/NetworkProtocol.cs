using LiteNetLib.Utils;

namespace JungleBackend.Protocol;

public enum JoinAuthMode : byte { RoomCode = 0, Jwt = 1 }
public sealed record JoinRequest(ushort Version, JoinAuthMode Mode, string RoomCode, string DisplayName)
{
    // Không để record.ToString() vô tình in mã phòng ra log.
    public override string ToString() => $"JoinRequest(version={Version}, mode={Mode})";
}

/// <summary>Hợp đồng wire dùng chung qua Compile Link; thay thế hoàn toàn key smoke SRV-01.</summary>
public static class NetworkProtocol
{
    public const ushort Version = 1;
    public const byte Ping = 1, Pong = 2, Auth = 3, JoinAccepted = 4, JoinRejected = 5, AuthRequired = 6;
    public const int PacketBytes = 5, MaxConnectBytes = 256, MaxAuthBytes = 4096;
    public const int MaxNameChars = 32, MaxRoomChars = 64, MaxTokenChars = 2048;

    public static NetDataWriter WriteConnect(JoinAuthMode mode, string roomCode, string displayName)
    {
        if (string.IsNullOrWhiteSpace(roomCode) || roomCode.Length > MaxRoomChars
            || string.IsNullOrWhiteSpace(displayName) || displayName.Length > MaxNameChars)
            throw new ArgumentException("Mã phòng cần 1–64 ký tự, tên hiển thị cần 1–32 ký tự.");
        var writer = new NetDataWriter();
        writer.Put(Version);
        writer.Put((byte)mode);
        writer.Put(roomCode);
        writer.Put(displayName);
        // Chặn byte thực tế: Unicode có thể chiếm nhiều byte dù số ký tự vẫn đúng.
        if (writer.Length > MaxConnectBytes) throw new ArgumentException("Connection request vượt 256 byte.");
        return writer;
    }

    public static bool TryReadConnect(NetDataReader reader, out JoinRequest? request, out string reason)
    {
        request = null;
        reason = reader.AvailableBytes > MaxConnectBytes ? "PayloadTooLarge" : "MalformedRequest";
        if (reader.AvailableBytes > MaxConnectBytes || !reader.TryGetUShort(out var version)
            || !reader.TryGetByte(out var mode) || !reader.TryGetString(out var room)
            || !reader.TryGetString(out var name) || reader.AvailableBytes != 0) return false;
        if (mode > (byte)JoinAuthMode.Jwt) { reason = "AuthModeMismatch"; return false; }
        request = new JoinRequest(version, (JoinAuthMode)mode, room, name.Trim());
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
