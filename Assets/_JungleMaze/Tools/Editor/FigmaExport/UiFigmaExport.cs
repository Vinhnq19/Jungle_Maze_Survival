using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Flow;
using JungleMaze.App.Hud.Mocks;
using JungleMaze.App.Hud.State;
using JungleMaze.App.Lobby;
using JungleMaze.App.Lobby.Mocks;
using JungleMaze.App.MatchUi;
using JungleMaze.App.MatchUi.Mocks;
using JungleMaze.App.Ports;
using JungleMaze.Core.Events;
using JungleMaze.Domain.Lobby;
using JungleMaze.Domain.MatchUi;
using JungleMaze.Domain.Network;
using JungleMaze.Infrastructure.Config;
using JungleMaze.Presentation.UI;
using JungleMaze.Presentation.UI.Hud;
using JungleMaze.Presentation.UI.Lobby;
using JungleMaze.Presentation.UI.MatchOverlay;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace JungleMaze.EditorTools
{
    /// <summary>
    /// Exports the 34 UI screen states of Docs/Figma/Screens_2026-10-31 as layer trees for an editable Figma rebuild.
    /// Real prefabs in a temporary preview scene, mock data, canvases at 1920×1080 through a camera; every active,
    /// visible RectTransform becomes a node (screen pixels, top-left origin) with its image and text, and every sprite
    /// used is written once as PNG. Editor only: no scene is saved, no prefab or runtime code is touched.
    /// </summary>
    public static class UiFigmaExport
    {
        public const string OutDir = "Docs/Figma/Export_2026-10-31/";
        private const int W = 1920, H = 1080;

        [MenuItem("Jungle Maze/Figma/Export UI Layers")]
        public static void Export()
        {
            Directory.CreateDirectory(OutDir + "sprites");
            using (var s = new Session())
            {
                ExportLobby(s);
                ExportMatch(s);
                s.WriteSprites();
                Debug.Log("[FigmaExport] " + s.Screens + " screens, " + s.SpriteCount + " sprites → " + OutDir);
            }
        }

        // ---------- UI-02 ----------

        private static void ExportLobby(Session s)
        {
            var go = s.Spawn("Assets/_JungleMaze/Prefabs/UI/Lobby/LobbyUi.prefab", 5);
            var root = go.GetComponent<LobbyUiRoot>();
            double now = 100;
            Func<double> clock = () => now;
            var sc = new LobbyScenarios(clock);
            sc.Use(LobbyCase.HappyPath);
            var p = sc.Ports;
            var hist = new ExportHistory();
            var account = new AccountSession(p, () => DateTime.UtcNow);
            var options = new LobbyOptions();
            var flow = new LobbyFlow(p, p, p, p, new JoinController(p, clock, options, "Huy"), account, new RoomListModel(p, account),
                new HistoryModel(hist, () => account.IsValid ? account.Token : null, () => true, options), new LeaderboardModel(p),
                new ApiStatusModel(p), clock, options);
            var presenter = new LobbyUiPresenter(root, flow, null, () => { }, _ => { }, null);
            root.ServerSettings.Host.text = "127.0.0.1";
            root.ServerSettings.Port.text = "7777";
            root.ServerSettings.Api.text = "http://127.0.0.1:5000";
            root.ServerSettings.Status.text = "Profile phase2-local";
            void Shot(string name) { flow.Tick(0.016f); presenter.Tick(); s.Dump(name); }
            void Slots()
            {
                sc.Lobby.SlotList.Clear();
                string[] names = { "Huy", "Huy", "Mai", "Lan", "Tuấn" };
                for (byte i = 0; i < names.Length; i++) sc.Lobby.SlotList.Add(new LobbySlot(i, "s" + i, names[i], i == 0));
                sc.Lobby.PlayerCount = names.Length; sc.Lobby.OwnNetId = 0; sc.Lobby.MinPlayers = 6; sc.Lobby.MaxPlayers = 10; sc.Lobby.RoomCode = "K7M2Q9";
            }
            MatchParticipantState P(byte id, byte rank, string n, uint sec) =>
                new MatchParticipantState { NetId = id, Rank = rank, DisplayName = n, PlayerSessionId = "s" + id, SurvivalTicks = 30u * sec };
            MatchResultView Result(bool win) => new MatchResultView
            {
                MatchId = sc.Lobby.MatchId, Reason = MatchEndReason.LastAlive, WinnerNetId = win ? (byte)0 : (byte)2, OwnNetId = 0, ParticipantCount = 5,
                Participants = new[] { P(0, win ? (byte)1 : (byte)3, "Huy", win ? 412u : 265u), P(1, win ? (byte)2 : (byte)4, "Huy", 388),
                    P(2, win ? (byte)3 : (byte)1, "Mai", 301), P(3, win ? (byte)4 : (byte)2, "Lan", 190), P(4, 5, "Tuấn", 95) }
            };
            void Join()
            {
                flow.OpenJoin(); flow.SubmitJoin(); sc.Connection.Set(NetworkState.Connecting); flow.Tick(0.016f);
                sc.Connection.Set(NetworkState.Joined); sc.Lobby.MatchId = Guid.NewGuid(); sc.Lobby.ServerTick = 1000; Slots();
            }

            account.Username = "huycv"; account.Password = "secret123"; account.Submit(); // UI-02 round 2: login first
            Shot("01_main-menu");
            flow.OpenJoin(); flow.Join.SetName("Huy"); flow.Join.SetCode("K7M2Q9"); Shot("02_join-form");
            flow.Join.SetCode("K7M2QO"); root.JoinForm.RoomCode.SetTextWithoutNotify("K7M2QO"); Shot("03_join-form-invalid-code");
            flow.Join.SetCode("K7M2Q9"); root.JoinForm.RoomCode.SetTextWithoutNotify("K7M2Q9"); flow.Join.Rejected(DisconnectReason.RoomFull); Shot("04_join-rejected-room-full");
            flow.SubmitJoin(); sc.Connection.Set(NetworkState.Connecting); sc.Connection.Step = "UDP"; Shot("05_connecting");
            flow.CancelJoin(); sc.Connection.Set(NetworkState.Disconnected); flow.Back(); flow.Tick(0.016f); presenter.Tick();
            flow.RequestLogout(); flow.ConfirmLogout(); presenter.Tick(); root.Login.Username.text = "huycv"; Shot("06_login");
            account.RegisterMode = true; account.Username = "huycv"; account.Password = "abc"; account.Submit(); Shot("07_register-validation");
            account.RegisterMode = false; sc.Api.SeedRooms(2); account.Username = "huycv"; account.Password = "secret123"; account.Submit();
            flow.OpenRooms(); Shot("08_room-manager");
            var ownId = Guid.Empty;
            foreach (var r in flow.Rooms.Rooms) if (!r.IsDefault) { ownId = r.Id; break; }
            flow.Rooms.RequestDelete(ownId); Shot("09_room-delete-confirm");
            flow.Rooms.CancelDelete(); sc.Api.CreateStatus = 409; sc.Api.CreateError = "UserRoomLimitReached"; flow.Rooms.NewName = "Phòng thứ ba";
            flow.Rooms.Create(); root.RoomManager.NewName.text = "Phòng thứ ba"; Shot("10_room-create-limit");
            sc.Api.CreateStatus = 201; flow.Back(); flow.Tick(0.016f);
            Join(); sc.Lobby.SetPhase(MatchPhase.Lobby, 12); Shot("11_lobby-countdown");
            sc.Lobby.SetPhase(MatchPhase.Waiting, 0); sc.Lobby.SlotList.RemoveAt(4); sc.Lobby.PlayerCount = 4; Shot("12_lobby-waiting");
            Slots(); sc.Lobby.SetPhase(MatchPhase.Loading, 24); Shot("13_loading");
            sc.Lobby.LoadError = "ConfigHash khác server"; Shot("14_loading-error");
            sc.Lobby.LoadError = null; flow.GoHome(); sc.Connection.Set(NetworkState.Disconnected); flow.Tick(0.016f);
            Join(); sc.Lobby.SetPhase(MatchPhase.InGame, 0); flow.Tick(0.016f);
            sc.Results.Current = Result(true); sc.Lobby.SetPhase(MatchPhase.Ending, 10); Shot("15_results-victory");
            flow.Replay(); Shot("16_results-replay-waiting");
            sc.Lobby.MatchId = Guid.NewGuid(); sc.Results.Current = null; sc.Lobby.SetPhase(MatchPhase.InGame, 0); flow.Tick(0.016f); flow.Tick(0.016f);
            sc.Results.Current = Result(false); sc.Lobby.SetPhase(MatchPhase.Ending, 10); Shot("17_results-defeat");
            sc.Lobby.MatchId = Guid.NewGuid(); sc.Results.Current = null; sc.Lobby.SetPhase(MatchPhase.InGame, 0); flow.Tick(0.016f);
            sc.Connection.CanResume = true; sc.Connection.Set(NetworkState.Disconnected, DisconnectReason.ConnectionLost); flow.Tick(0.016f);
            now += 4; Shot("18_reconnecting");
            now += 12; Shot("19_reconnect-failed");
            flow.GoHome(); flow.Tick(0.016f); sc.Connection.CanResume = false;
            hist.Data.Add(new PlayerMatchHistory { MatchId = Guid.NewGuid(), Rank = 1, SurvivalSeconds = 412, EndedAt = DateTimeOffset.Now.AddMinutes(-20), Reason = "LastAlive" });
            hist.Data.Add(new PlayerMatchHistory { MatchId = Guid.NewGuid(), Rank = 4, SurvivalSeconds = 190, EndedAt = DateTimeOffset.Now.AddHours(-2), Reason = "LastAlive" });
            hist.Data.Add(new PlayerMatchHistory { MatchId = Guid.NewGuid(), Rank = 2, SurvivalSeconds = 355, EndedAt = DateTimeOffset.Now.AddDays(-1), Reason = "Timeout" });
            flow.OpenHistory(); Shot("20_history");
            hist.Data.Clear(); _ = flow.History.Refresh(); Shot("21_history-empty");
            hist.Fail = true; _ = flow.History.Refresh(); Shot("22_history-unavailable");
            sc.Api.Board = new[] { new LeaderboardEntry("s2", "Mai", 1, 0, 412), new LeaderboardEntry("s0", "Huy", 2, 0, 388),
                new LeaderboardEntry("s1", "Huy", 3, 0, 301), new LeaderboardEntry("s3", "Lan", 4, 0, 190) };
            flow.OpenLeaderboard(Guid.NewGuid()); Shot("23_leaderboard");
            sc.Api.LeaderboardStatus = 404; flow.Leaderboard.Load(Guid.NewGuid()); Shot("24_leaderboard-empty");
            sc.Api.LeaderboardStatus = 503; flow.Leaderboard.Load(Guid.NewGuid()); Shot("25_leaderboard-unavailable");
            presenter.Dispose();
            flow.Dispose();
            Object.DestroyImmediate(go);
        }

        // ---------- UI-01 HUD + UI-03 overlay ----------

        private static void ExportMatch(Session s)
        {
            const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
            var balance = new BalanceConfigProvider(Application.streamingAssetsPath, null);
            var bus = new EventBus();
            var hud = new HudStateStore(bus, balance);
            var inv = new InventoryStateStore(bus, balance);
            var eff = new EffectStateStore(bus);
            hud.Initialize(); inv.Initialize(); eff.Initialize();
            var mhud = new MockHudState(hud, balance);
            new HudMockScenarios(mhud, new MockInventoryState(inv, balance), new MockEffectState(eff)).MockupV2();
            var art = Resources.Load<UiArtSet>(UiArtSet.ResourcePath);
            IItemIconProvider icons = art != null && art.ItemIcons != null ? (IItemIconProvider)art.ItemIcons : new NullItemIconProvider();
            var hudGo = s.Spawn("Assets/_JungleMaze/Prefabs/UI/Hud.prefab", 20);
            var hudRoot = hudGo.GetComponent<HudRoot>();
            hudRoot.Construct(hud, inv, eff, icons);
            typeof(HudRoot).GetMethod("Start", Private)?.Invoke(hudRoot, null); // Start does not run in edit mode
            var fb = hudGo.GetComponent<HudFeedbackRoot>();
            if (fb != null)
            {
                fb.Construct(hud, inv, eff);
                fb.ConstructIcons(icons);
                typeof(HudFeedbackRoot).GetMethod("Start", Private)?.Invoke(fb, null);
            }
            double now = 50;
            Func<double> clock = () => now;
            var menu = new MatchMenuController(bus, clock, () => Guid.Empty, AppScreen.InMatch);
            var liveE = new MockEliminationSource();
            var liveS = new MockSpectatorSource();
            var liveR = new UnavailableKillcamReplaySource();
            var killcam = new KillcamSession(clock, new KillcamOptions(false, 5f, 1f), liveR);
            var oflow = new MatchOverlayFlow(bus, clock, liveE, liveS, AppScreen.InMatch, null, killcam);
            var sc = new MatchOverlayScenarios(oflow, menu, killcam, liveE, liveS, liveR, clock);
            sc.UseMocks(true);
            sc.Populate(6);
            var ovGo = s.Spawn("Assets/_JungleMaze/Prefabs/UI/MatchOverlay/MatchOverlay.prefab", 5);
            var ov = ovGo.GetComponent<MatchOverlayRoot>();
            var opres = new MatchOverlayPresenter(ov, menu, oflow, null, null, null, balance.Match.HeroMaxHp);
            ov.Bind(opres, new MatchOverlayInput(menu, oflow));
            void Step(int frames, float dt)
            {
                for (var i = 0; i < frames; i++)
                {
                    now += dt;
                    hud.Tick(dt); inv.Tick(dt); eff.Tick(dt);
                    hudRoot.Presenter?.Tick(dt);
                    fb?.Presenter?.Tick(dt, 0, 0);
                    oflow.Tick(dt); menu.Tick(dt); killcam.Tick(); opres.Tick(dt);
                }
            }
            void Shot(string name) { Step(3, 0.05f); s.Dump(name); }

            Step(20, 0.05f);
            Shot("26_hud-in-match");
            mhud.Pickup("CROSSBOW", 1); mhud.Pickup("GOD_LEAF", 2); Step(2, 0.05f); Shot("27_hud-pickup-toast");
            mhud.SetHp(18); mhud.Zone(9f); Step(10, 0.05f); Shot("28_hud-low-hp-zone");
            mhud.SetHp(72); mhud.Zone(84f); Step(30, 0.1f);
            menu.Open(); Step(10, 0.05f); Shot("29_esc-menu");
            menu.RequestLeave(); Step(10, 0.05f); Shot("30_leave-confirm");
            menu.CancelLeave(); menu.Close(); Step(10, 0.05f);
            sc.Eliminate(EliminationCause.Player); Step(30, 0.05f); Shot("31_eliminated");
            oflow.Spectate(); Step(20, 0.05f); Shot("32_spectator");
            sc.EndPreview(); Step(5, 0.05f); sc.PreviewMatch(); Step(5, 0.05f);
            sc.SetKillcam(true); sc.SetReplayCase(MockKillcamReplaySource.Case.Full);
            sc.Eliminate(EliminationCause.Player); Step(30, 0.05f); Shot("33_killcam");
            sc.EndPreview(); Step(5, 0.05f); sc.PreviewMatch(); Step(5, 0.05f);
            sc.SetReplayCase(MockKillcamReplaySource.Case.None);
            sc.Eliminate(EliminationCause.Player); Step(30, 0.05f); Shot("34_killcam-unavailable-notice");

            opres.Dispose(); oflow.Dispose(); menu.Dispose(); hud.Dispose(); inv.Dispose(); eff.Dispose();
            Object.DestroyImmediate(hudGo);
            Object.DestroyImmediate(ovGo);
        }

        // ---------- capture session ----------

        private sealed class Session : IDisposable
        {
            private readonly Scene _scene;
            private readonly Camera _cam;
            private readonly RenderTexture _rt;
            private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
            private readonly Dictionary<Sprite, string> _spriteNames = new Dictionary<Sprite, string>();
            private readonly List<GameObject> _owned = new List<GameObject>();

            public int Screens { get; private set; }
            public int SpriteCount => _sprites.Count;

            public Session()
            {
                _scene = EditorSceneManager.NewPreviewScene();
                _rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
                var camGo = new GameObject("FigmaExportCamera");
                SceneManager.MoveGameObjectToScene(camGo, _scene);
                _owned.Add(camGo);
                _cam = camGo.AddComponent<Camera>();
                _cam.scene = _scene;
                _cam.targetTexture = _rt;
                _cam.orthographic = true;
                _cam.clearFlags = CameraClearFlags.SolidColor;
            }

            public GameObject Spawn(string prefabPath, float planeDistance)
            {
                var go = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
                SceneManager.MoveGameObjectToScene(go, _scene);
                var canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = _cam;
                canvas.planeDistance = planeDistance;
                return go;
            }

            public void Dump(string name)
            {
                Canvas.ForceUpdateCanvases();
                foreach (var root in _scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<TMP_Text>(true)) t.ForceMeshUpdate();
                Canvas.ForceUpdateCanvases();
                var canvases = new List<Canvas>();
                foreach (var root in _scene.GetRootGameObjects())
                {
                    var c = root.GetComponent<Canvas>();
                    if (c != null && c.enabled && root.activeInHierarchy) canvases.Add(c);
                }
                canvases.Sort((a, b) => a.sortingOrder.CompareTo(b.sortingOrder)); // back to front
                var sb = new StringBuilder();
                sb.Append("{\"name\":").Append(Str(name)).Append(",\"x\":0,\"y\":0,\"w\":").Append(W).Append(",\"h\":").Append(H)
                  .Append(",\"alpha\":1,\"image\":null,\"text\":null,\"children\":[");
                var first = true;
                foreach (var c in canvases)
                    first = Node(sb, (RectTransform)c.transform, 1f, c.rootCanvas.scaleFactor, first);
                sb.Append("]}");
                File.WriteAllText(OutDir + name + ".json", sb.ToString(), new UTF8Encoding(false));
                Screens++;
            }

            /// <summary>Appends the node and its children; returns the updated "first item" flag.</summary>
            private bool Node(StringBuilder sb, RectTransform rt, float parentAlpha, float scale, bool first)
            {
                if (!rt.gameObject.activeInHierarchy) return first;
                var alpha = parentAlpha;
                var group = rt.GetComponent<CanvasGroup>();
                if (group != null && group.enabled) alpha = (group.ignoreParentGroups ? 1f : parentAlpha) * group.alpha;
                if (alpha <= 0.001f) return first;

                var graphic = rt.GetComponent<Graphic>();
                if (graphic != null && !graphic.enabled) graphic = null;
                var visible = graphic != null && graphic.color.a * alpha > 0.001f;

                var body = new StringBuilder();
                var any = false;
                foreach (Transform child in rt)
                    if (child is RectTransform crt) any = !Node(body, crt, alpha, scale, !any) || any;
                if (!visible && !any) return first;

                if (!first) sb.Append(',');
                var r = ScreenRect(rt);
                sb.Append("{\"name\":").Append(Str(rt.name))
                  .Append(",\"x\":").Append(F(r.x)).Append(",\"y\":").Append(F(r.y))
                  .Append(",\"w\":").Append(F(r.width)).Append(",\"h\":").Append(F(r.height))
                  .Append(",\"alpha\":").Append(F(alpha));
                sb.Append(",\"image\":");
                if (visible && graphic is Image image) WriteImage(sb, image);
                else if (visible && graphic is RawImage raw) sb.Append("{\"sprite\":null,\"texture\":").Append(Str(raw.texture != null ? raw.texture.name : null))
                    .Append(",\"color\":").Append(Str(Hex(raw.color))).Append(",\"type\":\"Simple\",\"border\":[0,0,0,0],\"fillAmount\":1}");
                else sb.Append("null");
                sb.Append(",\"text\":");
                if (visible && graphic is TMP_Text tmp) WriteText(sb, tmp.text, tmp.fontSize * scale, tmp.color, tmp.alignment.ToString(),
                    (tmp.fontStyle & FontStyles.Bold) != 0 || (tmp.font != null && tmp.font.name.Contains("Bold")), tmp.font != null ? tmp.font.name : null);
                else if (visible && graphic is Text legacy) WriteText(sb, legacy.text, legacy.fontSize * scale, legacy.color, legacy.alignment.ToString(),
                    legacy.fontStyle == FontStyle.Bold || legacy.fontStyle == FontStyle.BoldAndItalic, legacy.font != null ? legacy.font.name : null);
                else sb.Append("null");
                sb.Append(",\"children\":[").Append(body).Append("]}");
                return false;
            }

            private void WriteImage(StringBuilder sb, Image image)
            {
                var sprite = image.sprite;
                var b = sprite != null ? sprite.border : Vector4.zero; // x=left, y=bottom, z=right, w=top
                sb.Append("{\"sprite\":").Append(Str(sprite != null ? Register(sprite) : null))
                  .Append(",\"color\":").Append(Str(Hex(image.color)))
                  .Append(",\"type\":").Append(Str(image.type.ToString()))
                  .Append(",\"border\":[").Append(F(b.x)).Append(',').Append(F(b.w)).Append(',').Append(F(b.z)).Append(',').Append(F(b.y)).Append(']')
                  .Append(",\"fillAmount\":").Append(F(image.fillAmount)).Append('}');
            }

            private static void WriteText(StringBuilder sb, string content, float size, Color color, string align, bool bold, string font) =>
                sb.Append("{\"content\":").Append(Str(content)).Append(",\"fontSize\":").Append(F(size))
                  .Append(",\"color\":").Append(Str(Hex(color))).Append(",\"align\":").Append(Str(align))
                  .Append(",\"bold\":").Append(bold ? "true" : "false").Append(",\"font\":").Append(Str(font)).Append('}');

            private string Register(Sprite sprite)
            {
                if (_spriteNames.TryGetValue(sprite, out var known)) return known;
                var name = Safe(sprite.name);
                for (var i = 2; _sprites.ContainsKey(name); i++) name = Safe(sprite.name) + "_" + i;
                _sprites[name] = sprite;
                _spriteNames[sprite] = name;
                return name;
            }

            public void WriteSprites()
            {
                var list = new StringBuilder();
                var names = new List<string>(_sprites.Keys);
                names.Sort(StringComparer.Ordinal);
                foreach (var name in names)
                {
                    var sprite = _sprites[name];
                    var tex = sprite.texture;
                    var rect = sprite.textureRect;
                    var tmp = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    Graphics.Blit(tex, tmp);
                    var prev = RenderTexture.active;
                    RenderTexture.active = tmp;
                    var png = new Texture2D(Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height), TextureFormat.RGBA32, false);
                    png.ReadPixels(new Rect(rect.x, rect.y, png.width, png.height), 0, 0);
                    png.Apply();
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(tmp);
                    File.WriteAllBytes(OutDir + "sprites/" + name + ".png", png.EncodeToPNG());
                    var b = sprite.border;
                    list.Append(name).Append(".png\t").Append(png.width).Append('×').Append(png.height)
                        .Append("\tborder l,t,r,b=").Append(b.x).Append(',').Append(b.w).Append(',').Append(b.z).Append(',').Append(b.y)
                        .Append('\t').Append(AssetDatabase.GetAssetPath(sprite)).Append('\n');
                    Object.DestroyImmediate(png);
                }
                File.WriteAllText(OutDir + "sprites.txt", list.ToString(), new UTF8Encoding(false));
            }

            private Rect ScreenRect(RectTransform rt)
            {
                var corners = new Vector3[4];
                rt.GetWorldCorners(corners);
                float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                foreach (var corner in corners)
                {
                    var p = _cam.WorldToScreenPoint(corner);
                    minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                    minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
                }
                return new Rect(minX, H - maxY, maxX - minX, maxY - minY);
            }

            public void Dispose()
            {
                foreach (var go in _owned) if (go != null) Object.DestroyImmediate(go);
                _rt.Release();
                Object.DestroyImmediate(_rt);
                EditorSceneManager.ClosePreviewScene(_scene);
            }

            private static string F(float v) => Math.Round(v, 2).ToString(CultureInfo.InvariantCulture);

            private static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGBA(c);

            private static string Safe(string name)
            {
                foreach (var bad in Path.GetInvalidFileNameChars()) name = name.Replace(bad, '_');
                return name.Replace(' ', '_');
            }

            private static string Str(string s)
            {
                if (s == null) return "null";
                var sb = new StringBuilder("\"");
                foreach (var ch in s)
                {
                    switch (ch)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default:
                            if (ch < 0x20) sb.Append("\\u").Append(((int)ch).ToString("x4"));
                            else sb.Append(ch);
                            break;
                    }
                }
                return sb.Append('"').ToString();
            }
        }

        /// <summary>History source with switchable data/failure for the three history states.</summary>
        private sealed class ExportHistory : IPlayerHistory
        {
            public bool Fail;
            public readonly List<PlayerMatchHistory> Data = new List<PlayerMatchHistory>();

            public string Status { get; private set; } = "";
            public bool IsReady => true;
            public IReadOnlyList<PlayerMatchHistory> Rows { get; private set; } = Array.Empty<PlayerMatchHistory>();

            public Task<bool> EnsureGuestAsync(string baseUrl, CancellationToken cancellation = default) => Task.FromResult(true);

            public Task<string> TicketAsync(Guid attemptId, string roomCode, string accountToken = null, CancellationToken cancellation = default) =>
                Task.FromResult<string>(null);

            public Task RefreshHistoryAsync(string accountToken = null, CancellationToken cancellation = default)
            {
                Status = Fail ? "Lịch sử online không khả dụng" : "Lịch sử đã cập nhật";
                Rows = Fail ? (IReadOnlyList<PlayerMatchHistory>)Array.Empty<PlayerMatchHistory>() : Data.ToArray();
                return Task.CompletedTask;
            }

            public void Dispose() { }
        }
    }
}
