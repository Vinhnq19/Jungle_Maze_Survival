namespace GameServer.Networking;

/// <summary>Transport không sở hữu room/identity. Chỉ SessionHub thay đổi dữ liệu phiên.</summary>
public interface IGamePeer
{
    string Transport { get; }
    bool IsOpen { get; }
    bool TrySend(byte[] message, bool reliable);
    void Close(string reason);
}
