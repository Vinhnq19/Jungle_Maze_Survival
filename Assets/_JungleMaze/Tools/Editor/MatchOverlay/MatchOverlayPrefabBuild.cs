using System.Collections.Generic;
using System.IO;
using JungleMaze.Presentation.UI.Hud;
using JungleMaze.Presentation.UI.MatchOverlay;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JungleMaze.EditorTools
{
    /// <summary>
    /// Repeatable assembly of MatchOverlay.prefab and MatchOverlaySet.asset (UI-03), run from the menu or Unity MCP.
    /// Coordinates are Figma pixels on the 1280×720 reference canvas, y down from the top-left. Art follows
    /// Docs/Art/Production_A/MatchOverlay/asset_mapping.json: the 19 overlay sprites plus the reused HUD and menu
    /// sprites, tinted from the HUD palette; icons and overlays never change a rect (AC47).
    /// </summary>
    public static class MatchOverlayPrefabBuild
    {
        public const string PrefabFolder = "Assets/_JungleMaze/Prefabs/UI/MatchOverlay/";
        public const string PrefabPath = PrefabFolder + "MatchOverlay.prefab";
        public const string RowPrefabPath = PrefabFolder + "SpectatorRow.prefab";
        public const string SetFolder = "Assets/_JungleMaze/Art/UI/MatchOverlay/Resources/";
        public const string SetPath = SetFolder + "MatchOverlaySet.asset";

        public const string OverlayArt = "Assets/_JungleMaze/Art/UI/MatchOverlay/";
        private const string HudArt = "Assets/_JungleMaze/Art/Hud/";
        private const string MenuArt = "Assets/_JungleMaze/Art/UI/Menu/";

        /// <summary>The 19 overlay sprites with their 9-slice border (px, all sides), from asset_mapping.json.</summary>
        public static readonly IReadOnlyDictionary<string, int> OverlaySprites = new Dictionary<string, int>
        {
            { "SliderHandle", 0 }, { "ToggleBox", 12 }, { "ToggleCheck", 0 }, { "ZoneRing", 0 }, { "KillcamGrid", 0 },
            { "PlayerDot", 0 }, { "PlayerDotOutline", 0 }, { "KillerMark", 0 }, { "CausePlayer", 0 }, { "CauseZone", 0 },
            { "CauseLeft", 0 }, { "CauseDisconnect", 0 }, { "CauseUnknown", 0 }, { "IconVolume", 0 }, { "IconMusic", 0 },
            { "IconMouse", 0 }, { "IconShake", 0 }, { "IconExit", 0 }, { "IconSpectate", 0 }
        };

        /// <summary>Tiled across the killcam board, so it repeats instead of clamping.</summary>
        public const string TiledSprite = "KillcamGrid";
        public const float IconSize = 28f, IconInset = 36f;

        private const int TextSize = 15;
        private static readonly HudPalette P = HudPalette.Default;
        private static TMP_FontAsset TextFont => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_JungleMaze/Art/Fonts/TMP/HudText SDF.asset");
        private static TMP_FontAsset NumberFont => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_JungleMaze/Art/Fonts/TMP/HudNumber SDF.asset");

        [MenuItem("Jungle Maze/UI-03/Build Match Overlay")]
        public static void Build()
        {
            ConfigureImporters();
            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(SetFolder);
            BuildRowPrefab();
            var root = new GameObject("MatchOverlay", typeof(RectTransform));
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 20; // above the HUD (10)
                var scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                root.AddComponent<GraphicRaycaster>();
                var overlay = root.AddComponent<MatchOverlayRoot>();
                var rt = (RectTransform)root.transform;

                BuildPanels(rt, overlay);

                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(rt, false);
                events.SetActive(false); // switched on at composition only when the scene has none
                Set(overlay, "canvas", canvas);
                Set(overlay, "eventSystem", events);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                var set = AssetDatabase.LoadAssetAtPath<MatchOverlaySet>(SetPath);
                if (set == null)
                {
                    set = ScriptableObject.CreateInstance<MatchOverlaySet>();
                    AssetDatabase.CreateAsset(set, SetPath);
                }
                Set(set, "overlayPrefab", prefab.GetComponent<MatchOverlayRoot>());
                EditorUtility.SetDirty(set);
                AssetDatabase.SaveAssets();
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>Sprite (2D and UI), Single, PPU 100, Point, no mipmaps, uncompressed, pivot centre, FullRect, border per table.</summary>
        [MenuItem("Jungle Maze/UI-03/Import Overlay Sprites")]
        public static void ConfigureImporters()
        {
            foreach (var pair in OverlaySprites)
            {
                var path = OverlayArt + pair.Key + ".png";
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                {
                    Debug.LogError("[UI-03] Missing overlay sprite " + path);
                    continue;
                }
                var border = new Vector4(pair.Value, pair.Value, pair.Value, pair.Value);
                var wrap = pair.Key == TiledSprite ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                var dirty = importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single
                    || importer.filterMode != FilterMode.Point || importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed
                    || !Mathf.Approximately(importer.spritePixelsPerUnit, 100) || importer.wrapMode != wrap || settings.spriteBorder != border
                    || settings.spriteMeshType != SpriteMeshType.FullRect || settings.spriteAlignment != (int)SpriteAlignment.Center;
                if (!dirty) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.wrapMode = wrap;
                importer.spritePixelsPerUnit = 100;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.spritePivot = new Vector2(0.5f, 0.5f);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteBorder = border;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }

        private static void BuildPanels(RectTransform rt, MatchOverlayRoot overlay)
        {
            // Sibling order is draw order: killcam board at the back, the ESC menu and its confirm box on top.
            Set(overlay, "killcam", BuildKillcam(rt));
            Set(overlay, "spectator", BuildSpectator(rt));
            Set(overlay, "eliminated", BuildEliminated(rt));
            Set(overlay, "notice", BuildNotice(rt));
            Set(overlay, "menu", BuildMenu(rt));
            Set(overlay, "confirm", BuildConfirm(rt));
        }

        private static RectTransform Screen(string name, RectTransform parent)
        {
            var r = Rect(name, parent, 0, 0, 1280, 720);
            Stretch(r);
            return r;
        }

        // ---------- Eliminated (Figma v2 43:283, AC45) ----------

        private static EliminatedView BuildEliminated(RectTransform parent)
        {
            var root = Screen("Eliminated", parent);
            var group = Group(root);
            var view = root.gameObject.AddComponent<EliminatedView>();
            var frame = Rect("Frame", root, 320, 141, 640, 417);
            Panel(frame);
            var title = Txt("Title", frame, 32, 28, 576, 40, "Bạn đã bị loại", 28, TextFont, TextAlignmentOptions.Center, P.Red);
            title.fontStyle = FontStyles.Bold;
            var rank = Txt("Rank", frame, 32, 84, 576, 64, "#4 / 10", 48, NumberFont, TextAlignmentOptions.Center, P.Amber);
            var cause = Txt("Cause", frame, 32, 164, 576, 28, "Không rõ nguyên nhân", 17, TextFont, TextAlignmentOptions.Center, P.Text);
            cause.margin = new Vector4(IconInset, 0, 0, 0); // room for the icon, so icon + text stay centred
            var causeIcon = Icon(cause.rectTransform, 0, 0, Overlay("CauseUnknown"));
            causeIcon.rectTransform.pivot = new Vector2(1f, 0.5f);
            causeIcon.rectTransform.anchoredPosition = new Vector2(0, -14);
            var kills = Txt("Kills", frame, 32, 204, 576, 24, "Hạ gục: 0", TextSize, TextFont, TextAlignmentOptions.Center, P.Muted);
            var survival = Txt("Survival", frame, 32, 236, 576, 24, "Sống sót: 00:00", TextSize, TextFont, TextAlignmentOptions.Center, P.Muted);
            var blocked = Txt("BlockedReason", frame, 32, 330, 576, 24, "Không còn người chơi nào để theo dõi", 13, TextFont, TextAlignmentOptions.Center, P.Muted);
            // Absolute Figma positions: "Về menu" (461,508), "Theo dõi người khác" (599,508).
            var home = Btn("Home", frame, 461 - 320, 508 - 141, 126, 50, "Về menu", Secondary, P.Text);
            var spectate = Btn("Spectate", frame, 599 - 320, 508 - 141, 220, 48, "Theo dõi người khác", Primary, P.Text);
            Set(view, "group", group);
            Set(view, "frame", frame);
            Set(view, "title", title);
            Set(view, "rank", rank);
            Set(view, "cause", cause);
            Set(view, "kills", kills);
            Set(view, "survival", survival);
            Set(view, "blockedReason", blocked);
            Set(view, "home", home);
            Set(view, "spectate", spectate);
            Set(view, "causeIcon", causeIcon);
            // Index = EliminationCause: Unknown, Player, Zone, LeftMatch, Disconnected.
            SetArray(view, "causeIcons", new Object[]
                { Overlay("CauseUnknown"), Overlay("CausePlayer"), Overlay("CauseZone"), Overlay("CauseLeft"), Overlay("CauseDisconnect") });
            return view;
        }

        // ---------- Spectator (Figma v1 7:304, AC46; list G-12) ----------

        private static void BuildRowPrefab()
        {
            var row = Rect("SpectatorRow", null, 0, 0, 160, 32);
            try
            {
                var bg = Img(row, P.Surface, true);
                Skin(bg, Hud("HudRound6"), 2f); // 32 px tall row, 20 px border
                Layer(row, "ArtEdge", Hud("AFrameEdgeSlot128"), P.Edge, 2f);
                Layer(row, "ArtBevel", Hud("ABevelSlot128"), P.Line, 2f);
                var outline = Layer(row, "Selected", Hud("HudRound6Stroke2"), P.Amber, 2f);
                outline.enabled = false;
                var button = row.gameObject.AddComponent<Button>();
                button.targetGraphic = bg;
                var label = Txt("Label", row, 10, 0, 146, 32, "Người chơi", TextSize, TextFont, TextAlignmentOptions.MidlineLeft, P.Text);
                var view = row.gameObject.AddComponent<SpectatorRowView>();
                Set(view, "button", button);
                Set(view, "background", bg);
                Set(view, "label", label);
                Set(view, "selectedOutline", outline);
                PrefabUtility.SaveAsPrefabAsset(row.gameObject, RowPrefabPath);
            }
            finally { Object.DestroyImmediate(row.gameObject); }
        }

        private static SpectatorView BuildSpectator(RectTransform parent)
        {
            var root = Screen("Spectator", parent);
            var group = Group(root);
            var view = root.gameObject.AddComponent<SpectatorView>();

            var block = Rect("Watching", root, 430, 24, 420, 71);
            Skin(Img(block, P.Surface, true), Hud("HudRound10"), 1f);
            Icon(block, 16, (71 - IconSize) / 2f, Overlay("IconSpectate"));
            var caption = Txt("Caption", block, 16, 8, 200, 18, "Đang theo dõi", 13, TextFont, TextAlignmentOptions.MidlineLeft, P.Muted);
            caption.margin = new Vector4(IconInset, 0, 0, 0);
            var name = Txt("Name", block, 16, 28, 260, 34, "Người chơi", 22, TextFont, TextAlignmentOptions.MidlineLeft, P.Text);
            name.margin = new Vector4(IconInset, 0, 0, 0);
            name.fontStyle = FontStyles.Bold;
            var alive = Txt("Alive", block, 270, 8, 134, 56, "Còn sống 9/10", TextSize, NumberFont, TextAlignmentOptions.MidlineRight, P.Amber);

            var list = Rect("List", root, 24, 130, 160, 9 * 38 - 6);
            var rowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RowPrefabPath);
            var rows = new Object[9];
            for (var i = 0; i < rows.Length; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(rowPrefab, list);
                var r = (RectTransform)go.transform;
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
                r.anchoredPosition = new Vector2(0, -i * 38);
                go.name = "Row" + (i + 1);
                rows[i] = go.GetComponent<SpectatorRowView>();
            }

            var card = Rect("TargetCard", root, 24, 560, 300, 90);
            Panel(card);
            var cardName = Txt("Name", card, 16, 10, 268, 24, "Người chơi", 17, TextFont, TextAlignmentOptions.MidlineLeft, P.Text);
            var hp = Rect("Hp", card, 16, 40, 268, 18);
            var track = Rect("Track", hp, 0, 4, 200, 10);
            Skin(Img(track, P.Track), Hud("BarTrack"), 2f); // 10 px tall, 6 px border
            var fill = Rect("Fill", track, 0, 0, 200, 10);
            Stretch(fill);
            Skin(Img(fill, P.Red), Hud("BarFill"), 2f);
            var hpText = Txt("Value", hp, 208, 0, 60, 18, "100/100", 13, NumberFont, TextAlignmentOptions.MidlineRight, P.Text);
            var weapon = Txt("Weapon", card, 16, 62, 268, 20, "Súng nước", 13, TextFont, TextAlignmentOptions.MidlineLeft, P.Muted);

            var keys = Rect("KeyBar", root, 440, 640, 400, 44);
            Skin(Img(keys, P.Surface), Hud("HudRound6"), 1f);
            Txt("Keys", keys, 12, 0, 376, 44, "Trái/A  Phải/D đổi người · E kẻ hạ bạn · ESC menu", 13, TextFont, TextAlignmentOptions.MidlineLeft, P.Text);
            var killer = Btn("Killer", root, 852, 640, 250, 44, "Kẻ đã hạ bạn (E)", Secondary, P.Text);
            var home = Btn("Home", root, 1114, 640, 100, 47, "Về sảnh", Primary, P.Text);

            Set(view, "group", group);
            Set(view, "watchingBlock", block);
            Set(view, "watchingName", name);
            Set(view, "aliveCount", alive);
            Set(view, "keyBar", keys);
            Set(view, "killer", killer);
            Set(view, "home", home);
            Set(view, "card", card);
            Set(view, "cardName", cardName);
            Set(view, "cardHp", hp.gameObject);
            Set(view, "cardHpFill", fill);
            Set(view, "cardHpText", hpText);
            Set(view, "cardWeapon", weapon);
            SetArray(view, "rows", rows);
            return view;
        }

        // ---------- Killcam (T-5: own flat board, no world) ----------

        private static KillcamView BuildKillcam(RectTransform parent)
        {
            var root = Screen("Killcam", parent);
            var group = Group(root);
            Img(root, new Color(0.047f, 0.078f, 0.090f, 0.97f), true);
            var view = root.gameObject.AddComponent<KillcamView>();
            var board = Centered("Board", root, 1280, 720);
            var grid = Img(board, P.Grid);
            grid.sprite = Overlay(TiledSprite);
            grid.type = Image.Type.Tiled; // 48 px per tile, never one stretched tile
            var zone = Centered("Zone", board, 480, 480);
            Img(zone, new Color(0x95 / 255f, 0x72 / 255f, 0xBA / 255f, 0.85f)).sprite = Overlay("ZoneRing");
            var dots = new Object[10];
            var images = new Object[10];
            var names = new Object[10];
            var fills = new Object[10];
            var marks = new Object[10];
            for (var i = 0; i < 10; i++)
            {
                var dot = Centered("Player" + i, board, 18, 18);
                var image = Img(dot, P.Text);
                image.sprite = Overlay("PlayerDot");
                Layer(dot, "Outline", Overlay("PlayerDotOutline"), P.Edge, 1f).type = Image.Type.Simple; // rim only, no overlap
                var mark = Layer(dot, "KillerMark", Overlay("KillerMark"), P.Amber, 1f);
                mark.type = Image.Type.Simple;
                mark.gameObject.SetActive(false);
                marks[i] = mark.gameObject;
                var label = Txt("Name", dot, -51, -24, 120, 18, "#" + (i + 1), 13, TextFont, TextAlignmentOptions.Center, P.Text);
                var track = Rect("Hp", dot, -3, 22, 24, 4);
                Skin(Img(track, P.Track), Hud("BarTrack"), 3f); // 4 px tall, 6 px border
                var fill = Rect("Fill", track, 0, 0, 24, 4);
                Stretch(fill);
                Skin(Img(fill, P.Red), Hud("BarFill"), 3f);
                dot.gameObject.SetActive(false);
                dots[i] = dot;
                images[i] = image;
                names[i] = label;
                fills[i] = fill;
            }
            var title = Txt("Title", root, 340, 24, 600, 28, "Killcam · góc nhìn của kẻ hạ bạn", 17, TextFont, TextAlignmentOptions.Center, P.Text);
            var countdown = Txt("Countdown", root, 490, 56, 300, 40, "Killcam 5,0 s", 26, NumberFont, TextAlignmentOptions.Center, P.Amber);
            var skip = Btn("Skip", root, 540, 640, 200, 48, "Bỏ qua (Space)", Secondary, P.Text);
            Set(view, "group", group);
            Set(view, "board", board);
            Set(view, "zone", zone);
            SetArray(view, "dots", dots);
            SetArray(view, "dotImages", images);
            SetArray(view, "names", names);
            SetArray(view, "hpFills", fills);
            SetArray(view, "killerMarks", marks);
            Set(view, "title", title);
            Set(view, "countdown", countdown);
            Set(view, "skip", skip);
            return view;
        }

        // ---------- notice line ----------

        private static OverlayNoticeView BuildNotice(RectTransform parent)
        {
            var box = Rect("Notice", parent, 390, 108, 500, 36);
            var group = Group(box);
            Skin(Img(box, P.Surface), Hud("ToastBg"), 2f); // 36 px tall, 20 px border
            var text = Txt("Text", box, 12, 0, 476, 36, "", TextSize, TextFont, TextAlignmentOptions.Center, P.Text);
            var view = box.gameObject.AddComponent<OverlayNoticeView>();
            Set(view, "group", group);
            Set(view, "text", text);
            return view;
        }

        // ---------- ESC menu (G-10: 560 px wide, centred, ≤ 60 % of the height) ----------

        private const float MenuWidth = 560f, MenuHeight = 388f, RowHeight = 48f;

        private static MatchMenuView BuildMenu(RectTransform parent)
        {
            var panel = Centered("MenuPanel", parent, MenuWidth, MenuHeight);
            var group = Group(panel);
            Panel(panel);
            var view = panel.gameObject.AddComponent<MatchMenuView>();
            Set(view, "group", group);
            var title = Txt("Title", panel, 16, 16, 400, 32, "Cài đặt", 20, TextFont, TextAlignmentOptions.MidlineLeft, P.Text);
            title.fontStyle = FontStyles.Bold;

            var y = 56f;
            var master = SliderRow(panel, "Master", "Âm lượng tổng", "IconVolume", y, 0f, 1f, false, out var masterValue);
            var sfx = SliderRow(panel, "Sfx", "Hiệu ứng", "IconVolume", y += RowHeight, 0f, 1f, false, out var sfxValue);
            var music = SliderRow(panel, "Music", "Nhạc", "IconMusic", y += RowHeight, 0f, 1f, false, out var musicValue);
            var musicRow = music.transform.parent;
            var musicToggle = Tgl("MusicEnabled", musicRow, 470, 12, 74, 24, "Bật");
            var musicOff = Txt("MusicOff", musicRow, 150, 30, 250, 16, "Nhạc nền đang tắt", 13, TextFont, TextAlignmentOptions.MidlineLeft, P.Muted);
            var sensitivity = SliderRow(panel, "Sensitivity", "Độ nhạy chuột", "IconMouse", y += RowHeight, 5f, 20f, true, out var sensitivityValue);
            var shakeRow = Row(panel, "ShakeRow", y += RowHeight);
            Icon(shakeRow, 16, (RowHeight - IconSize) / 2f, Overlay("IconShake"));
            Txt("Label", shakeRow, 16, 0, 200, RowHeight, "Rung màn hình", TextSize, TextFont, TextAlignmentOptions.MidlineLeft, P.Text)
                .margin = new Vector4(IconInset, 0, 0, 0);
            var shake = Tgl("ScreenShake", shakeRow, 470, 12, 74, 24, "Bật");

            y += RowHeight + 12f;
            var defaults = Btn("Defaults", panel, 16, y, 140, 48, "Mặc định", Secondary, P.Text);
            var resume = Btn("Resume", panel, 168, y, 160, 48, "Tiếp tục", Primary, P.Text);
            var leave = Btn("Leave", panel, 364, y, 180, 48, "Thoát trận", Secondary, P.Red);
            ButtonIcon(leave, Overlay("IconExit"));
            var retry = Txt("RetryNotice", panel, 16, y + 52, 528, 20, "Chưa thoát được, thử lại", 13, TextFont, TextAlignmentOptions.MidlineLeft, P.Yellow);
            retry.gameObject.SetActive(false);

            Set(view, "master", master);
            Set(view, "sfx", sfx);
            Set(view, "music", music);
            Set(view, "sensitivity", sensitivity);
            Set(view, "masterValue", masterValue);
            Set(view, "sfxValue", sfxValue);
            Set(view, "musicValue", musicValue);
            Set(view, "sensitivityValue", sensitivityValue);
            Set(view, "musicEnabled", musicToggle);
            Set(view, "screenShake", shake);
            Set(view, "musicOffLabel", musicOff);
            Set(view, "resume", resume);
            Set(view, "defaults", defaults);
            Set(view, "leave", leave);
            Set(view, "retryNotice", retry);
            return view;
        }

        private static Slider SliderRow(RectTransform panel, string name, string label, string icon, float y, float min, float max, bool whole, out TMP_Text value)
        {
            var row = Row(panel, name + "Row", y);
            Icon(row, 16, (RowHeight - IconSize) / 2f, Overlay(icon));
            var text = Txt("Label", row, 16, 0, 130, RowHeight, label, TextSize, TextFont, TextAlignmentOptions.MidlineLeft, P.Text);
            text.margin = new Vector4(IconInset, 0, 0, 0);
            text.textWrappingMode = TextWrappingModes.Normal; // a long label takes two lines beside the icon, rect unchanged
            var slider = Sld(name, row, 150, 10, 250, 20, min, max, whole);
            value = Txt("Value", row, 410, 0, 54, RowHeight, whole ? "1,0" : "100%", TextSize, NumberFont, TextAlignmentOptions.MidlineRight, P.Text);
            return slider;
        }

        private static RectTransform Row(RectTransform panel, string name, float y) => Rect(name, panel, 0, y, MenuWidth, RowHeight);

        // ---------- leave confirm (Q7) ----------

        private static LeaveConfirmView BuildConfirm(RectTransform parent)
        {
            var root = Rect("LeaveConfirm", parent, 0, 0, 1280, 720);
            Stretch(root);
            var group = Group(root);
            Img(root, new Color(0f, 0f, 0f, 0.55f), true);
            var box = Centered("Box", root, 420, 190);
            Panel(box);
            var view = root.gameObject.AddComponent<LeaveConfirmView>();
            var title = Txt("Title", box, 20, 18, 380, 30, "Thoát trận?", 20, TextFont, TextAlignmentOptions.MidlineLeft, P.Text);
            title.fontStyle = FontStyles.Bold;
            var message = Txt("Message", box, 20, 52, 380, 44, "Bạn sẽ bị loại khỏi trận.", TextSize, TextFont, TextAlignmentOptions.TopLeft, P.Muted);
            message.textWrappingMode = TextWrappingModes.Normal;
            var stay = Btn("Stay", box, 20, 122, 180, 48, "Ở lại", Primary, P.Text);
            var confirm = Btn("Confirm", box, 220, 122, 180, 48, "Thoát", Secondary, P.Red);
            ButtonIcon(confirm, Overlay("IconExit"));
            Set(view, "group", group);
            Set(view, "message", message);
            Set(view, "confirm", confirm);
            Set(view, "stay", stay);
            return view;
        }

        // ---------- helpers (y down from the parent's top-left) ----------

        internal static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
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

        internal static RectTransform Centered(string name, Transform parent, float w, float h)
        {
            var r = Rect(name, parent, 0, 0, w, h);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = Vector2.zero;
            return r;
        }

        internal static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        internal static CanvasGroup Group(RectTransform r)
        {
            var g = r.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0f;
            g.interactable = false;
            g.blocksRaycasts = false;
            return g;
        }

        internal static Image Img(RectTransform r, Color color, bool raycast = false)
        {
            var i = r.gameObject.AddComponent<Image>();
            i.color = color;
            i.raycastTarget = raycast;
            return i;
        }

        internal static TMP_Text Txt(string name, Transform parent, float x, float y, float w, float h, string text, float size,
            TMP_FontAsset font, TextAlignmentOptions align, Color color)
        {
            var r = Rect(name, parent, x, y, w, h);
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.fontSize = size;
            t.enableAutoSizing = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            t.text = text;
            t.alignment = align;
            t.color = color;
            return t;
        }

        internal static Button Btn(string name, Transform parent, float x, float y, float w, float h, string label, Sprite face, Color fg)
        {
            var r = Rect(name, parent, x, y, w, h);
            var image = Img(r, Color.white, true);
            Skin(image, face, 2f); // 44–50 px tall, 32 px border
            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = image;
            var text = Txt("Label", r, 0, 0, w, h, label, TextSize, TextFont, TextAlignmentOptions.Center, fg);
            text.fontStyle = FontStyles.Bold;
            return b;
        }

        internal static Slider Sld(string name, Transform parent, float x, float y, float w, float h, float min, float max, bool whole)
        {
            var r = Rect(name, parent, x, y, w, h);
            var background = Rect("Background", r, 0, h * 0.35f, w, h * 0.3f);
            Skin(Img(background, P.Track, true), Hud("BarTrack"), 2f); // 6 px tall, 6 px border
            var fillArea = Rect("FillArea", r, 0, h * 0.35f, w, h * 0.3f);
            var fill = Rect("Fill", fillArea, 0, 0, 0, h * 0.3f);
            Skin(Img(fill, P.Amber), Hud("BarFill"), 2f);
            var handleArea = Rect("HandleArea", r, 0, 0, w, h);
            var handle = Rect("Handle", handleArea, 0, 0, h, h);
            var handleImage = Img(handle, P.Text, true);
            handleImage.sprite = Overlay("SliderHandle");
            var s = r.gameObject.AddComponent<Slider>();
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0, 1); fill.pivot = new Vector2(0, 0.5f); fill.sizeDelta = Vector2.zero; fill.anchoredPosition = Vector2.zero;
            handle.anchorMin = Vector2.zero; handle.anchorMax = new Vector2(0, 1); handle.pivot = new Vector2(0.5f, 0.5f); handle.sizeDelta = new Vector2(h, 0); handle.anchoredPosition = Vector2.zero;
            s.fillRect = fill;
            s.handleRect = handle;
            s.targetGraphic = handleImage;
            s.direction = Slider.Direction.LeftToRight;
            s.minValue = min;
            s.maxValue = max;
            s.wholeNumbers = whole;
            s.value = max;
            return s;
        }

        internal static Toggle Tgl(string name, Transform parent, float x, float y, float w, float h, string label)
        {
            var r = Rect(name, parent, x, y, w, h);
            var box = Rect("Background", r, 0, 0, h, h);
            var bg = Img(box, P.Line, true);
            Skin(bg, Overlay("ToggleBox"), 2f); // 24 px box, 12 px border
            var check = Rect("Checkmark", box, 4, 4, h - 8, h - 8);
            var checkImage = Img(check, P.Amber);
            checkImage.sprite = Overlay("ToggleCheck");
            Txt("Label", r, h + 8, 0, w - h - 8, h, label, TextSize, TextFont, TextAlignmentOptions.MidlineLeft, P.Text);
            var t = r.gameObject.AddComponent<Toggle>();
            t.targetGraphic = bg;
            t.graphic = checkImage;
            t.isOn = true;
            return t;
        }

        // ---------- art (asset_mapping.json) ----------

        private static Sprite Primary => Load(MenuArt + "ButtonPrimary.png");
        private static Sprite Secondary => Load(MenuArt + "ButtonSecondary.png");
        private static Sprite Overlay(string name) => Load(OverlayArt + name + ".png");
        private static Sprite Hud(string name) => Load(HudArt + name + ".png");

        private static Sprite Load(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogError("[UI-03] Missing sprite " + path);
            return sprite;
        }

        /// <summary>Sliced sprite on an existing Image; the multiplier shrinks the border to fit short rects.</summary>
        private static Image Skin(Image image, Sprite sprite, float pixelsPerUnitMultiplier)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
            return image;
        }

        /// <summary>A stretched, non-raycast overlay child over <paramref name="parent"/>'s own Image (edge, bevel, selection).</summary>
        private static Image Layer(RectTransform parent, string name, Sprite sprite, Color color, float pixelsPerUnitMultiplier)
        {
            var r = Rect(name, parent, 0, 0, 0, 0);
            Stretch(r);
            return Skin(Img(r, color), sprite, pixelsPerUnitMultiplier);
        }

        /// <summary>HudRound10 base tinted Surface, then dark edge and light bevel (base → edge → bevel), behind the content.</summary>
        private static void Panel(RectTransform block)
        {
            Skin(Img(block, P.Surface, true), Hud("HudRound10"), 1f);
            Layer(block, "ArtEdge", Hud("AFrameEdge96"), P.Edge, 1f);
            Layer(block, "ArtBevel", Hud("ABevel96"), P.Line, 1f);
        }

        /// <summary>A 28 px colored icon at (x, y) in <paramref name="parent"/>, untinted.</summary>
        private static Image Icon(RectTransform parent, float x, float y, Sprite sprite)
        {
            var image = Img(Rect("Icon", parent, x, y, IconSize, IconSize), Color.white);
            image.sprite = sprite;
            image.preserveAspect = true;
            return image;
        }

        /// <summary>Icon at the button's left edge; the label keeps centring in the space right of it.</summary>
        private static void ButtonIcon(Button button, Sprite sprite)
        {
            var r = (RectTransform)button.transform;
            Icon(r, 14, (r.sizeDelta.y - IconSize) / 2f, sprite);
            button.GetComponentInChildren<TMP_Text>().margin = new Vector4(IconInset, 0, 0, 0);
        }

        internal static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var array = so.FindProperty(field);
            array.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
