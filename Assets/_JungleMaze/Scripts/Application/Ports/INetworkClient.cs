using System;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Config;
using JungleMaze.Domain.Network;
using LiteNetLib.Utils;

namespace JungleMaze.App.Ports
{
    /// <summary>Thông tin một lượt vào phòng; token chỉ giữ trong RAM, không ghi log.</summary>
    public readonly struct ConnectRequest
    {
        public readonly ClientConfig Config;
        public readonly string RoomCode;
        public readonly string DisplayName;
        public readonly string AccessToken;

        public ConnectRequest(ClientConfig config, string roomCode, string displayName, string accessToken = null)
        {
            Config = config;
            RoomCode = roomCode;
            DisplayName = displayName;
            AccessToken = accessToken;
        }
    }

    /// <summary>Port kết nối. Infrastructure implement bằng LiteNetLib; Application/Presentation không biết transport.</summary>
    public interface INetworkClient
    {
        NetworkState State { get; }
        DisconnectReason LastReason { get; }
        string PlayerId { get; }
        string Endpoint { get; }
        NetworkStats Stats { get; }

        /// <summary>Chỉ hợp lệ khi Disconnected/Failed; lỗi config báo bằng state Failed + InvalidConfig.</summary>
        void Connect(in ConnectRequest request);

        /// <summary>Ngắt chủ động, trả slot cho server; không tự reconnect.</summary>
        void Disconnect();
    }

    /// <summary>Gửi packet gameplay; chỉ gửi được khi Joined. Trả false nếu chưa vào phòng.</summary>
    public interface IPacketSender
    {
        bool Send<T>(ref T packet, PacketDelivery delivery) where T : struct, INetSerializable;
    }

    /// <summary>Nhận packet gameplay ở main thread; nhiều subscriber cho cùng một loại packet.</summary>
    public interface IPacketReceiver
    {
        void Subscribe<T>(Action<T> handler) where T : struct, INetSerializable;
        void Unsubscribe<T>(Action<T> handler) where T : struct, INetSerializable;
    }
}
