using System.Collections.Generic;
using JungleMaze.App.Config;

namespace JungleMaze.App.Ports
{
    /// <summary>Nguồn config ngoài build. Implement ở Infrastructure (StreamingAssets, persistentDataPath, command line).</summary>
    public interface IConfigProvider
    {
        /// <summary>Config đang dùng; có thể không hợp lệ — gọi Validate() trước khi Connect.</summary>
        ClientConfig Current { get; }

        /// <summary>Tên các profile đi kèm build, ví dụ "local", "lan", "online".</summary>
        IReadOnlyList<string> Profiles { get; }

        /// <summary>Nạp một profile đi kèm build làm Current. Trả lỗi hoặc null.</summary>
        string SelectProfile(string profile);

        /// <summary>Lưu config người dùng sửa trên menu để lần mở sau dùng lại, không cần build lại.</summary>
        string SaveUserConfig(ClientConfig config);

        /// <summary>Tham số dòng lệnh -room/-name (tiện mở nhiều client test); null nếu không truyền.</summary>
        string CommandLineRoomCode { get; }
        string CommandLineDisplayName { get; }
    }
}
