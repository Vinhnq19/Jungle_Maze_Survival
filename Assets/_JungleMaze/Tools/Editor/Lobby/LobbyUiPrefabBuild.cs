using System.IO;
using JungleMaze.Presentation.UI.Hud;
using JungleMaze.Presentation.UI.Lobby;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JungleMaze.EditorTools
{
    /// <summary>
    /// Repeatable assembly of LobbyUi.prefab and LobbyUiSet.asset (UI-02), run from the menu or Unity MCP, in the same
    /// way as the UI-03 overlay builder. Coordinates are Figma v2 pixels on the 1280×720 canvas, y down from the top
    /// left (Join panel 42:137 at (360,67) 560×586; Results table 43:445 at (760,123) 440×475, buttons at y 399).
    /// Placeholder look: flat palette colours plus the six menu sprites; art is only ever assigned here (AC50).
    /// Rows are built inside the prefab (10 lobby slots, 5 rooms, 10 result/leaderboard rows, 20 history lines).
    /// </summary>
    public static class LobbyUiPrefabBuild
    {
        public const string PrefabFolder = "Assets/_JungleMaze/Prefabs/UI/Lobby/";
        public const string PrefabPath = PrefabFolder + "LobbyUi.prefab";
        public const string SetFolder = "Assets/_JungleMaze/Art/UI/Lobby/Resources/";
        public const string SetPath = SetFolder + "LobbyUiSet.asset";
        private const string MenuArt = "Assets/_JungleMaze/Art/UI/Menu/";
        public const float ButtonHeight = 48f, InputHeight = 44f;

        private static readonly HudPalette P = HudPalette.Default;
        private static TMP_FontAsset TextFont => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_JungleMaze/Art/Fonts/TMP/HudText SDF.asset");
        private static TMP_FontAsset NumberFont => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_JungleMaze/Art/Fonts/TMP/HudNumber SDF.asset");
        private static Sprite Menu(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(MenuArt + name + ".png");

        [MenuItem("Jungle Maze/UI-02/Build Lobby UI")]
        public static void Build()
        {
            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(SetFolder);
            var go = new GameObject("LobbyUi", typeof(RectTransform));
            try
            {
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 15; // above the HUD (10), below the UI-03 overlay (20) so its settings panel sits on top
                var scaler = go.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                go.AddComponent<GraphicRaycaster>();
                var root = go.AddComponent<LobbyUiRoot>();
                var rt = (RectTransform)go.transform;

                var backdrop = Rect("Backdrop", rt, 0, 0, 0, 0);
                Stretch(backdrop);
                var bg = Img(backdrop, Color.white);
                bg.sprite = Menu("Background");
                bg.raycastTarget = false;
                Set(root, "canvas", canvas);
                Set(root, "backdrop", backdrop.gameObject);
                Set(root, "mainMenu", BuildMainMenu(rt));
                Set(root, "joinForm", BuildJoin(rt));
                Set(root, "connecting", BuildConnecting(rt));
                Set(root, "login", BuildLogin(rt));
                Set(root, "roomManager", BuildRooms(rt));
                Set(root, "history", BuildHistory(rt));
                Set(root, "leaderboard", BuildLeaderboard(rt));
                Set(root, "lobby", BuildLobby(rt));
                Set(root, "loading", BuildLoading(rt));
                Set(root, "results", BuildResults(rt));
                Set(root, "reconnect", BuildReconnect(rt));
                Set(root, "notice", BuildNotice(rt));

                var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                var set = AssetDatabase.LoadAssetAtPath<LobbyUiSet>(SetPath);
                if (set == null)
                {
                    set = ScriptableObject.CreateInstance<LobbyUiSet>();
                    AssetDatabase.CreateAsset(set, SetPath);
                }
                Set(set, "uiPrefab", prefab.GetComponent<LobbyUiRoot>());
                EditorUtility.SetDirty(set);
                AssetDatabase.SaveAssets();
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ---------- panels ----------

        private static MainMenuView BuildMainMenu(RectTransform rt)
        {
            var v = Screen<MainMenuView>(rt, "MainMenu");
            var r = (RectTransform)v.transform;
            var logo = Img(Rect("Logo", r, 440, 48, 400, 140), Color.white);
            logo.sprite = Menu("Logo");
            logo.preserveAspect = true;
            logo.raycastTarget = false;
            Set(v, "join", Btn(r, "Join", "Vào phòng", 490, 216, 300, true));
            Set(v, "rooms", Btn(r, "Rooms", "Quản lý phòng", 490, 276, 300));
            Set(v, "history", Btn(r, "History", "Lịch sử", 490, 336, 300));
            Set(v, "settings", Btn(r, "Settings", "Cài đặt", 490, 396, 300));
            Set(v, "quit", Btn(r, "Quit", "Thoát", 490, 456, 300, false, true));
            Set(v, "apiStatus", Txt(r, "ApiStatus", "", 390, 520, 500, 28, 15, TextAlignmentOptions.Center, P.Muted));
            var box = Block(r, "Endpoint", 920, 216, 320, 352);
            Txt(box, "Title", "Máy chủ", 16, 12, 288, 24, 16, TextAlignmentOptions.Left, P.Text);
            Set(v, "profile", Drop(box, "Profile", 16, 44, 288));
            Set(v, "host", Input(box, "Host", "Host", 16, 96, 288));
            Set(v, "port", Input(box, "Port", "Port", 16, 148, 120));
            Set(v, "api", Input(box, "Api", "API (https://…)", 16, 200, 288));
            Set(v, "saveEndpoint", Btn(box, "Save", "Lưu endpoint", 16, 252, 288));
            Set(v, "endpointStatus", Txt(box, "Status", "", 16, 306, 288, 40, 13, TextAlignmentOptions.TopLeft, P.Muted));
            return v;
        }

        private static JoinFormView BuildJoin(RectTransform rt)
        {
            var v = Screen<JoinFormView>(rt, "JoinForm");
            var b = Block((RectTransform)v.transform, "Panel", 360, 67, 560, 586);
            Txt(b, "Title", "Vào phòng", 32, 24, 496, 36, 26, TextAlignmentOptions.Left, P.Text);
            Txt(b, "NameLabel", "Tên hiển thị", 32, 84, 496, 22, 15, TextAlignmentOptions.Left, P.Muted);
            Set(v, "displayName", Input(b, "DisplayName", "Tên của bạn (1–32 ký tự)", 32, 110, 496));
            Set(v, "nameError", Txt(b, "NameError", "", 32, 158, 496, 22, 14, TextAlignmentOptions.Left, P.Red));
            Txt(b, "CodeLabel", "Mã phòng (6 ký tự)", 32, 190, 496, 22, 15, TextAlignmentOptions.Left, P.Muted);
            var code = Input(b, "RoomCode", "VD: K7M2Q9", 32, 216, 496);
            code.textComponent.font = NumberFont;
            code.textComponent.fontSize = 24;
            code.textComponent.characterSpacing = 18;
            Set(v, "roomCode", code);
            Set(v, "codeError", Txt(b, "CodeError", "", 32, 264, 496, 22, 14, TextAlignmentOptions.Left, P.Red));
            Set(v, "message", Txt(b, "Message", "", 32, 300, 496, 60, 16, TextAlignmentOptions.TopLeft, P.Yellow));
            Set(v, "join", Btn(b, "Join", "Vào phòng", 32, 380, 496, true));
            Set(v, "retry", Btn(b, "Retry", "Thử lại", 32, 440, 240));
            Set(v, "back", Btn(b, "Back", "Về menu", 288, 440, 240, false, true));
            Txt(b, "Hint", "Không cần đăng nhập để vào phòng bằng mã.", 32, 520, 496, 40, 13, TextAlignmentOptions.TopLeft, P.Muted);
            return v;
        }

        private static ConnectingView BuildConnecting(RectTransform rt)
        {
            var v = Screen<ConnectingView>(rt, "Connecting");
            var b = Block((RectTransform)v.transform, "Panel", 440, 240, 400, 220);
            Set(v, "step", Txt(b, "Step", "Đang kết nối", 24, 32, 352, 36, 20, TextAlignmentOptions.Center, P.Text));
            Set(v, "rtt", Txt(b, "Rtt", "", 24, 76, 352, 24, 15, TextAlignmentOptions.Center, P.Muted));
            Set(v, "cancel", Btn(b, "Cancel", "Hủy", 100, 140, 200, false, true));
            return v;
        }

        private static LoginView BuildLogin(RectTransform rt)
        {
            var v = Screen<LoginView>(rt, "Login");
            var b = Block((RectTransform)v.transform, "Panel", 440, 110, 400, 480);
            Set(v, "title", Txt(b, "Title", "Đăng nhập", 24, 20, 352, 34, 24, TextAlignmentOptions.Left, P.Text));
            Set(v, "loginTab", Btn(b, "LoginTab", "Đăng nhập", 24, 66, 170));
            Set(v, "registerTab", Btn(b, "RegisterTab", "Đăng ký", 206, 66, 170));
            Set(v, "username", Input(b, "Username", "Tên đăng nhập (3–32)", 24, 136, 352));
            var pass = Input(b, "Password", "Mật khẩu", 24, 192, 352);
            pass.contentType = TMP_InputField.ContentType.Password;
            Set(v, "password", pass);
            Set(v, "message", Txt(b, "Message", "", 24, 248, 352, 60, 15, TextAlignmentOptions.TopLeft, P.Yellow));
            Set(v, "submit", Btn(b, "Submit", "Đăng nhập", 24, 320, 352, true));
            Set(v, "back", Btn(b, "Back", "Quay lại", 24, 384, 352, false, true));
            return v;
        }

        private static RoomManagerView BuildRooms(RectTransform rt)
        {
            var v = Screen<RoomManagerView>(rt, "RoomManager");
            var b = Block((RectTransform)v.transform, "Panel", 240, 48, 800, 624);
            Txt(b, "Title", "Quản lý phòng", 24, 16, 500, 34, 24, TextAlignmentOptions.Left, P.Text);
            Set(v, "logout", Btn(b, "Logout", "Đăng xuất", 616, 12, 160, false, true));
            Set(v, "state", Txt(b, "State", "", 24, 64, 600, 24, 15, TextAlignmentOptions.Left, P.Muted));
            Set(v, "retry", Btn(b, "Retry", "Thử lại", 640, 56, 136));
            var rows = new RoomRowView[5];
            for (var i = 0; i < rows.Length; i++) rows[i] = RoomRow(b, i, 100 + i * 64);
            SetArray(v, "rows", rows);
            Set(v, "newName", Input(b, "NewName", "Tên phòng (1–48)", 24, 440, 380));
            var cap = Input(b, "NewCapacity", "10", 420, 440, 100);
            cap.contentType = TMP_InputField.ContentType.IntegerNumber;
            cap.text = "10";
            Set(v, "newCapacity", cap);
            Set(v, "create", Btn(b, "Create", "Tạo phòng", 536, 438, 240, true));
            Set(v, "back", Btn(b, "Back", "Về menu", 24, 552, 200, false, true));
            var confirm = Block(b, "Confirm", 200, 220, 400, 180);
            var group = confirm.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = group.blocksRaycasts = false;
            Set(v, "confirmGroup", group);
            Set(v, "confirmText", Txt(confirm, "Text", "Xóa phòng này?", 20, 24, 360, 30, 18, TextAlignmentOptions.Center, P.Text));
            Set(v, "confirmYes", Btn(confirm, "Yes", "Xóa", 20, 100, 170, true));
            Set(v, "confirmNo", Btn(confirm, "No", "Giữ lại", 210, 100, 170, false, true));
            return v;
        }

        private static RoomRowView RoomRow(RectTransform parent, int index, float y)
        {
            var r = Rect("Room" + index, parent, 24, y, 752, 56);
            Img(r, P.Raised);
            var row = r.gameObject.AddComponent<RoomRowView>();
            Set(row, "roomName", Txt(r, "Name", "", 16, 6, 260, 44, 16, TextAlignmentOptions.Left, P.Text));
            Set(row, "occupancy", Txt(r, "Occupancy", "", 280, 6, 80, 44, 16, TextAlignmentOptions.Center, P.Muted, NumberFont));
            Set(row, "code", Txt(r, "Code", "", 364, 6, 120, 44, 18, TextAlignmentOptions.Center, P.Yellow, NumberFont));
            Set(row, "copy", Btn(r, "Copy", "Sao chép", 488, 4, 84, false, false, 44));
            Set(row, "join", Btn(r, "Join", "Vào", 578, 4, 80, true, false, 44));
            Set(row, "delete", Btn(r, "Delete", "Xóa", 664, 4, 80, false, true, 44));
            return row;
        }

        private static HistoryView BuildHistory(RectTransform rt)
        {
            var v = Screen<HistoryView>(rt, "History");
            var b = Block((RectTransform)v.transform, "Panel", 240, 48, 800, 624);
            Txt(b, "Title", "Lịch sử trận", 24, 16, 400, 34, 24, TextAlignmentOptions.Left, P.Text);
            Set(v, "identity", Txt(b, "Identity", "", 440, 20, 336, 28, 14, TextAlignmentOptions.Right, P.Muted));
            Set(v, "state", Txt(b, "State", "", 24, 56, 600, 24, 15, TextAlignmentOptions.Left, P.Muted));
            Set(v, "retry", Btn(b, "Retry", "Thử lại", 640, 52, 136, false, false, 44));
            var rows = new HistoryRowView[20];
            for (var i = 0; i < rows.Length; i++)
            {
                var r = Rect("Line" + i, b, 24, 104 + i * 22, 752, 22);
                var row = r.gameObject.AddComponent<HistoryRowView>();
                var button = r.gameObject.AddComponent<Button>();
                button.targetGraphic = Img(r, i % 2 == 0 ? P.Raised : P.Surface);
                Set(row, "open", button);
                Set(row, "line", Txt(r, "Text", "", 8, 0, 736, 22, 13, TextAlignmentOptions.Left, P.Text));
                rows[i] = row;
            }
            SetArray(v, "rows", rows);
            Set(v, "back", Btn(b, "Back", "Về menu", 24, 560, 200, false, true));
            return v;
        }

        private static LeaderboardView BuildLeaderboard(RectTransform rt)
        {
            var v = Screen<LeaderboardView>(rt, "Leaderboard");
            var b = Block((RectTransform)v.transform, "Panel", 340, 48, 600, 624);
            Txt(b, "Title", "Bảng xếp hạng trận (online)", 24, 16, 552, 34, 22, TextAlignmentOptions.Left, P.Text);
            Set(v, "state", Txt(b, "State", "", 24, 56, 400, 24, 15, TextAlignmentOptions.Left, P.Muted));
            Set(v, "retry", Btn(b, "Retry", "Thử lại", 440, 52, 136, false, false, 44));
            var rows = new ResultRowView[10];
            for (var i = 0; i < rows.Length; i++) rows[i] = ResultRowAt(b, i, 24, 100 + i * 44, 552);
            SetArray(v, "rows", rows);
            Set(v, "back", Btn(b, "Back", "Quay lại", 24, 556, 200, false, true));
            return v;
        }

        private static LobbyView BuildLobby(RectTransform rt)
        {
            var v = Screen<LobbyView>(rt, "Lobby");
            var r = (RectTransform)v.transform;
            var info = Block(r, "Info", 80, 123, 600, 475);
            Txt(info, "Title", "Phòng chờ", 24, 20, 300, 34, 24, TextAlignmentOptions.Left, P.Text);
            Txt(info, "CodeLabel", "Mã phòng", 24, 70, 200, 22, 15, TextAlignmentOptions.Left, P.Muted);
            Set(v, "roomCode", Txt(info, "RoomCode", "", 24, 94, 300, 48, 36, TextAlignmentOptions.Left, P.Yellow, NumberFont));
            Set(v, "copy", Btn(info, "Copy", "Sao chép", 340, 94, 160));
            Set(v, "count", Txt(info, "Count", "", 24, 160, 552, 28, 18, TextAlignmentOptions.Left, P.Text, NumberFont));
            Set(v, "status", Txt(info, "Status", "", 24, 200, 552, 36, 24, TextAlignmentOptions.Left, P.Green));
            Set(v, "rule", Txt(info, "Rule", "", 24, 244, 552, 24, 15, TextAlignmentOptions.Left, P.Muted));
            Set(v, "notice", Txt(info, "Notice", "", 24, 276, 552, 24, 15, TextAlignmentOptions.Left, P.Yellow));
            var ready = Btn(info, "Ready", "Sẵn sàng", 24, 330, 260, true);
            Set(v, "ready", ready);
            Set(v, "readyLabel", ready.GetComponentInChildren<TMP_Text>());
            Set(v, "start", Btn(info, "Start", "Bắt đầu (máy chủ chưa hỗ trợ)", 300, 330, 276));
            Set(v, "leave", Btn(info, "Leave", "Rời phòng", 24, 403, 260, false, true));
            var list = Block(r, "Slots", 760, 123, 440, 475);
            var slots = new LobbySlotView[10];
            for (var i = 0; i < slots.Length; i++)
            {
                var s = Rect("Slot" + i, list, 12, 12 + i * 45.2f, 416, 42);
                var slot = s.gameObject.AddComponent<LobbySlotView>();
                Set(slot, "background", Img(s, P.Raised));
                Set(slot, "playerName", Txt(s, "Name", "", 14, 0, 300, 42, 16, TextAlignmentOptions.Left, P.Text));
                Set(slot, "badge", Txt(s, "Badge", "", 316, 0, 90, 42, 14, TextAlignmentOptions.Right, P.Green));
                slots[i] = slot;
            }
            SetArray(v, "slots", slots);
            return v;
        }

        private static LoadingView BuildLoading(RectTransform rt)
        {
            var v = Screen<LoadingView>(rt, "Loading");
            var b = Block((RectTransform)v.transform, "Panel", 340, 140, 600, 440);
            Txt(b, "Title", "Đang tải trận", 24, 20, 552, 36, 26, TextAlignmentOptions.Left, P.Text);
            Set(v, "stepMap", Txt(b, "StepMap", "", 24, 80, 552, 28, 17, TextAlignmentOptions.Left, P.Text));
            Set(v, "stepVersion", Txt(b, "StepVersion", "", 24, 112, 552, 28, 17, TextAlignmentOptions.Left, P.Text));
            Set(v, "stepPlayers", Txt(b, "StepPlayers", "", 24, 144, 552, 28, 17, TextAlignmentOptions.Left, P.Text));
            Set(v, "timeLeft", Txt(b, "TimeLeft", "", 24, 192, 552, 36, 24, TextAlignmentOptions.Left, P.Green, NumberFont));
            Set(v, "hint", Txt(b, "Hint", "", 24, 252, 552, 28, 16, TextAlignmentOptions.Left, P.Muted));
            Set(v, "tip", Txt(b, "Tip", "", 24, 290, 552, 48, 15, TextAlignmentOptions.TopLeft, P.Muted));
            Set(v, "home", Btn(b, "Home", "Về menu", 24, 368, 200, false, true));
            return v;
        }

        private static ResultsView BuildResults(RectTransform rt)
        {
            var v = Screen<ResultsView>(rt, "Results");
            var r = (RectTransform)v.transform;
            var own = Block(r, "Own", 80, 123, 600, 260);
            Set(v, "title", Txt(own, "Title", "", 24, 20, 552, 52, 38, TextAlignmentOptions.Left, P.Yellow));
            Set(v, "rank", Txt(own, "Rank", "", 24, 88, 552, 36, 26, TextAlignmentOptions.Left, P.Text, NumberFont));
            Set(v, "kills", Txt(own, "Kills", "", 24, 130, 552, 26, 17, TextAlignmentOptions.Left, P.Text));
            Set(v, "survival", Txt(own, "Survival", "", 24, 160, 552, 26, 17, TextAlignmentOptions.Left, P.Text));
            Set(v, "cause", Txt(own, "Cause", "", 24, 192, 552, 26, 16, TextAlignmentOptions.Left, P.Muted));
            Set(v, "onlineLine", Txt(own, "OnlineLine", "", 24, 224, 552, 24, 14, TextAlignmentOptions.Left, P.Muted));
            var replay = Btn(r, "Replay", "Chơi lại", 80, 399, 290, true);
            Set(v, "replay", replay);
            Set(v, "replayLabel", replay.GetComponentInChildren<TMP_Text>());
            Set(v, "home", Btn(r, "Home", "Về menu", 390, 399, 290, false, true));
            Set(v, "online", Btn(r, "Online", "Xem online", 80, 463, 290));
            var table = Block(r, "Table", 760, 123, 440, 475);
            Txt(table, "Header", "Bảng xếp hạng", 12, 8, 416, 28, 17, TextAlignmentOptions.Left, P.Text);
            var rows = new ResultRowView[10];
            for (var i = 0; i < rows.Length; i++) rows[i] = ResultRowAt(table, i, 12, 42 + i * 42.5f, 416);
            SetArray(v, "rows", rows);
            return v;
        }

        private static ResultRowView ResultRowAt(RectTransform parent, int index, float x, float y, float w)
        {
            var r = Rect("Row" + index, parent, x, y, w, 40);
            var row = r.gameObject.AddComponent<ResultRowView>();
            var highlight = Img(r, P.SelectedFill);
            highlight.enabled = false;
            Set(row, "background", highlight);
            Set(row, "rank", Txt(r, "Rank", "", 8, 0, 56, 40, 17, TextAlignmentOptions.Left, P.Yellow, NumberFont));
            Set(row, "playerName", Txt(r, "Name", "", 68, 0, w - 220, 40, 16, TextAlignmentOptions.Left, P.Text));
            Set(row, "kills", Txt(r, "Kills", "", w - 148, 0, 60, 40, 16, TextAlignmentOptions.Center, P.Muted, NumberFont));
            Set(row, "survival", Txt(r, "Survival", "", w - 84, 0, 76, 40, 16, TextAlignmentOptions.Right, P.Muted, NumberFont));
            return row;
        }

        private static ReconnectView BuildReconnect(RectTransform rt)
        {
            var v = Screen<ReconnectView>(rt, "Reconnect");
            var r = (RectTransform)v.transform;
            Stretch(r);
            Img(r, new Color(0, 0, 0, 0.6f));
            var b = Block(r, "Panel", 390, 220, 500, 280);
            Set(v, "message", Txt(b, "Message", "Đang kết nối lại…", 24, 32, 452, 40, 24, TextAlignmentOptions.Center, P.Text));
            Set(v, "seconds", Txt(b, "Seconds", "", 24, 88, 452, 36, 22, TextAlignmentOptions.Center, P.Yellow, NumberFont));
            Set(v, "home", Btn(b, "Home", "Về menu", 150, 190, 200, false, true));
            return v;
        }

        private static NoticeView BuildNotice(RectTransform rt)
        {
            var v = Screen<NoticeView>(rt, "Notice");
            var b = Block((RectTransform)v.transform, "Panel", 390, 220, 500, 280);
            Set(v, "title", Txt(b, "Title", "", 24, 28, 452, 36, 24, TextAlignmentOptions.Center, P.Text));
            Set(v, "message", Txt(b, "Message", "", 24, 80, 452, 80, 17, TextAlignmentOptions.Top, P.Yellow));
            Set(v, "home", Btn(b, "Home", "Về menu", 150, 190, 200, true));
            return v;
        }

        // ---------- primitives ----------

        /// <summary>Full-screen panel with its CanvasGroup (shown/hidden by alpha only) and view component.</summary>
        private static T Screen<T>(RectTransform parent, string name) where T : LobbyPanelView
        {
            var r = Rect(name, parent, 0, 0, 0, 0);
            Stretch(r);
            var group = r.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = group.blocksRaycasts = false;
            var view = r.gameObject.AddComponent<T>();
            Set(view, "group", group);
            return view;
        }

        private static RectTransform Block(RectTransform parent, string name, float x, float y, float w, float h)
        {
            var r = Rect(name, parent, x, y, w, h);
            Img(r, P.Surface);
            var edge = Rect("Edge", r, 0, 0, 0, 0);
            Stretch(edge);
            var outline = edge.gameObject.AddComponent<Outline>();
            outline.effectColor = P.Line;
            outline.effectDistance = new Vector2(2, -2);
            var img = edge.gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0);
            img.raycastTarget = false;
            return r;
        }

        private static RectTransform Rect(string name, RectTransform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(w, h);
            return r;
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        private static Image Img(RectTransform r, Color color)
        {
            var image = r.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text Txt(RectTransform parent, string name, string text, float x, float y, float w, float h, float size,
            TextAlignmentOptions align, Color color, TMP_FontAsset font = null)
        {
            var t = Rect(name, parent, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font ? font : TextFont;
            t.text = text;
            t.fontSize = Mathf.Max(13f, size); // AC49: text ≥ 13 px
            t.alignment = align;
            t.color = color;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            return t;
        }

        private static Button Btn(RectTransform parent, string name, string label, float x, float y, float w,
            bool primary = false, bool ghost = false, float h = ButtonHeight)
        {
            var r = Rect(name, parent, x, y, w, Mathf.Max(44f, h)); // AC49: buttons ≥ 44 px
            var image = Img(r, Color.white);
            image.sprite = Menu(primary ? "ButtonPrimary" : ghost ? "ButtonGhost" : "ButtonSecondary");
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 2f;
            var button = r.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var text = Txt(r, "Label", label, 0, 0, 0, 0, 16, TextAlignmentOptions.Center, P.Text);
            Stretch((RectTransform)text.transform);
            return button;
        }

        private static TMP_InputField Input(RectTransform parent, string name, string placeholder, float x, float y, float w)
        {
            var go = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
            go.name = name;
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(w, InputHeight);
            go.GetComponent<Image>().color = P.Raised;
            var field = go.GetComponent<TMP_InputField>();
            field.textComponent.font = TextFont;
            field.textComponent.fontSize = 17;
            field.textComponent.color = P.Text;
            var hint = (TMP_Text)field.placeholder;
            hint.font = TextFont;
            hint.fontSize = 15;
            hint.text = placeholder;
            hint.color = P.Faint;
            return field;
        }

        private static TMP_Dropdown Drop(RectTransform parent, string name, float x, float y, float w)
        {
            var go = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
            go.name = name;
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(w, InputHeight);
            go.GetComponent<Image>().color = P.Raised;
            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
            {
                t.font = TextFont;
                t.fontSize = 15;
                t.color = t.transform.parent == r ? P.Text : Color.black;
            }
            go.GetComponent<TMP_Dropdown>().ClearOptions();
            return go.GetComponent<TMP_Dropdown>();
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var array = so.FindProperty(field);
            array.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
