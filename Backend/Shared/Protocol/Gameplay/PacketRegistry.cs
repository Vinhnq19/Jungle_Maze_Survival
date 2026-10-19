#nullable enable
using System;
using LiteNetLib.Utils;

namespace JungleBackend.Protocol.Gameplay
{
    /// <summary>
    /// Serializer packet gameplay dùng chung server/client, bọc NetPacketProcessor.
    /// Chỉ lo encode/decode: không biết peer, channel hay cách gửi. Mỗi loại packet có tối đa một handler;
    /// phía client NetworkClient tự phân phát tiếp cho nhiều subscriber.
    /// </summary>
    public sealed class PacketRegistry
    {
        private readonly NetPacketProcessor _processor = new NetPacketProcessor();

        /// <summary>Ghi header hash 8 byte + payload. Writer không bị Reset để có thể gộp nhiều packet.</summary>
        public void Write<T>(NetDataWriter writer, ref T packet) where T : struct, INetSerializable
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            _processor.WriteNetSerializable(writer, ref packet);
        }

        public void Subscribe<T>(Action<T> handler) where T : struct, INetSerializable
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _processor.SubscribeNetSerializable(handler);
        }

        /// <summary>Handler nhận kèm userData (server truyền session/peer của người gửi).</summary>
        public void Subscribe<T, TUserData>(Action<T, TUserData> handler) where T : struct, INetSerializable
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _processor.SubscribeNetSerializable(handler);
        }

        public bool Unsubscribe<T>() where T : struct, INetSerializable => _processor.RemoveSubscription<T>();

        /// <summary>
        /// Đọc hết packet trong reader. Trả false khi gặp hash lạ hoặc thiếu byte: packet hỏng từ mạng
        /// không được làm vỡ vòng PollEvents. Exception chỉ cấp phát ở đường lỗi, không ở đường thường.
        /// </summary>
        public bool TryReadAll(NetDataReader reader, object? userData = null)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            try
            {
                _processor.ReadAllPackets(reader, userData);
                return true;
            }
            catch (ParseException) { return false; }
            // LiteNetLib 2.x báo thiếu byte bằng InvalidOperationException ("Not enough data").
            catch (InvalidOperationException) { return false; }
            catch (IndexOutOfRangeException) { return false; }
            catch (ArgumentException) { return false; }
        }
    }
}
