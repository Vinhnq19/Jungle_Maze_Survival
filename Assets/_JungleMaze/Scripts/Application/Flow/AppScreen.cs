namespace JungleMaze.App.Flow
{
    /// <summary>Màn hình cấp ứng dụng; Presentation chọn view theo giá trị này.</summary>
    public enum AppScreen : byte
    {
        Boot = 0,
        MainMenu = 1,
        Connecting = 2,
        InRoom = 3,
        InMatch = 4,
        Results = 5
    }

    /// <summary>Publish mỗi lần GameFlow đổi state. Message là thông báo cho người dùng (lý do lỗi...).</summary>
    public readonly struct AppScreenChanged
    {
        public readonly AppScreen Previous;
        public readonly AppScreen Current;
        public readonly string Message;

        public AppScreenChanged(AppScreen previous, AppScreen current, string message)
        {
            Previous = previous;
            Current = current;
            Message = message;
        }
    }

    /// <summary>Lỗi trước khi vào được transport (config sai, login thất bại).</summary>
    public readonly struct ConnectionError
    {
        public readonly string Message;
        public ConnectionError(string message) => Message = message;
    }

    /// <summary>Tên scene và thời gian; Bootstrap có thể đổi mà không sửa state.</summary>
    public sealed class GameFlowSettings
    {
        public string MenuScene = "Boot";
        public string GameScene = "Game";
        public float ResultsSeconds = 5f;
        public bool StartLocalPreview;
    }
}
