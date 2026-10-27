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
    /// Coordinates are Figma pixels on the 1280×720 reference canvas, y down from the top-left. Placeholder art only:
    /// flat colors from the HUD palette and the HUD fonts; swapping art later edits the prefab, not code (AC47).
    /// </summary>
    public static class MatchOverlayPrefabBuild
    {
        public const string PrefabFolder = "Assets/_JungleMaze/Prefabs/UI/MatchOverlay/";
        public const string PrefabPath = PrefabFolder + "MatchOverlay.prefab";
        public const string SetFolder = "Assets/_JungleMaze/Art/UI/MatchOverlay/Resources/";
        public const string SetPath = SetFolder + "MatchOverlaySet.asset";

        private const int TextSize = 15;
        private static readonly HudPalette P = HudPalette.Default;
        private static TMP_FontAsset TextFont => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_JungleMaze/Art/Fonts/TMP/HudText SDF.asset");
        private static TMP_FontAsset NumberFont => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_JungleMaze/Art/Fonts/TMP/HudNumber SDF.asset");

        [MenuItem("Jungle Maze/UI-03/Build Match Overlay")]
        public static void Build()
        {
            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(SetFolder);
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

        private static void BuildPanels(RectTransform rt, MatchOverlayRoot overlay)
        {
            Set(overlay, "menu", BuildMenu(rt));
            Set(overlay, "confirm", BuildConfirm(rt));
        }

        // ---------- ESC menu (G-10: 560 px wide, centred, ≤ 60 % of the height) ----------

        private const float MenuWidth = 560f, MenuHeight = 388f, RowHeight = 48f;

        private static MatchMenuView BuildMenu(RectTransform parent)
        {
            var panel = Centered("MenuPanel", parent, MenuWidth, MenuHeight);
            var group = Group(panel);
            Img(panel, P.Surface, true);
            var view = panel.gameObject.AddComponent<MatchMenuView>();
            Set(view, "group", group);
            var title = Txt("Title", panel, 16, 16, 400, 32, "Cài đặt", 20, TextFont, TextAlignmentOptions.MidlineLeft, P.Text);
            title.fontStyle = FontStyles.Bold;

            var y = 56f;
            var master = SliderRow(panel, "Master", "Âm lượng tổng", y, 0f, 1f, false, out var masterValue);
            var sfx = SliderRow(panel, "Sfx", "Hiệu ứng", y += RowHeight, 0f, 1f, false, out var sfxValue);
            var music = SliderRow(panel, "Music", "Nhạc", y += RowHeight, 0f, 1f, false, out var musicValue);
            var musicRow = music.transform.parent;
            var musicToggle = Tgl("MusicEnabled", musicRow, 470, 12, 74, 24, "Bật");
            var musicOff = Txt("MusicOff", musicRow, 150, 30, 250, 16, "Nhạc nền đang tắt", 13, TextFont, TextAlignmentOptions.MidlineLeft, P.Muted);
            var sensitivity = SliderRow(panel, "Sensitivity", "Độ nhạy chuột", y += RowHeight, 5f, 20f, true, out var sensitivityValue);
            var shakeRow = Row(panel, "ShakeRow", y += RowHeight);
            Txt("Label", shakeRow, 16, 0, 200, RowHeight, "Rung màn hình", TextSize, TextFont, TextAlignmentOptions.MidlineLeft, P.Text);
            var shake = Tgl("ScreenShake", shakeRow, 470, 12, 74, 24, "Bật");

            y += RowHeight + 12f;
            var defaults = Btn("Defaults", panel, 16, y, 140, 48, "Mặc định", P.Raised, P.Text);
            var resume = Btn("Resume", panel, 168, y, 160, 48, "Tiếp tục", P.Amber, P.Raised);
            var leave = Btn("Leave", panel, 364, y, 180, 48, "Thoát trận", P.Red, P.Text);
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

        private static Slider SliderRow(RectTransform panel, string name, string label, float y, float min, float max, bool whole, out TMP_Text value)
        {
            var row = Row(panel, name + "Row", y);
            Txt("Label", row, 16, 0, 130, RowHeight, label, TextSize, TextFont, TextAlignmentOptions.MidlineLeft, P.Text);
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
            Img(box, P.Surface, true);
            var view = root.gameObject.AddComponent<LeaveConfirmView>();
            var title = Txt("Title", box, 20, 18, 380, 30, "Thoát trận?", 20, TextFont, TextAlignmentOptions.MidlineLeft, P.Text);
            title.fontStyle = FontStyles.Bold;
            var message = Txt("Message", box, 20, 52, 380, 44, "Bạn sẽ bị loại khỏi trận.", TextSize, TextFont, TextAlignmentOptions.TopLeft, P.Muted);
            message.textWrappingMode = TextWrappingModes.Normal;
            var stay = Btn("Stay", box, 20, 122, 180, 48, "Ở lại", P.Amber, P.Raised);
            var confirm = Btn("Confirm", box, 220, 122, 180, 48, "Thoát", P.Red, P.Text);
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

        internal static Button Btn(string name, Transform parent, float x, float y, float w, float h, string label, Color bg, Color fg)
        {
            var r = Rect(name, parent, x, y, w, h);
            var image = Img(r, bg, true);
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
            Img(background, P.Track, true);
            var fillArea = Rect("FillArea", r, 0, h * 0.35f, w, h * 0.3f);
            var fill = Rect("Fill", fillArea, 0, 0, 0, h * 0.3f);
            Img(fill, P.Amber);
            var handleArea = Rect("HandleArea", r, 0, 0, w, h);
            var handle = Rect("Handle", handleArea, 0, 0, h, h);
            var handleImage = Img(handle, P.Text, true);
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
            var bg = Img(box, P.Raised, true);
            var check = Rect("Checkmark", box, 4, 4, h - 8, h - 8);
            var checkImage = Img(check, P.Amber);
            Txt("Label", r, h + 8, 0, w - h - 8, h, label, TextSize, TextFont, TextAlignmentOptions.MidlineLeft, P.Text);
            var t = r.gameObject.AddComponent<Toggle>();
            t.targetGraphic = bg;
            t.graphic = checkImage;
            t.isOn = true;
            return t;
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
