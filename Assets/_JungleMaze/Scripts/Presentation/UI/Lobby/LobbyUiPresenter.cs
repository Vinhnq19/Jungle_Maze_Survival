using System;
using System.Globalization;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Lobby;
using JungleMaze.Domain.Lobby;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Lobby
{
    /// <summary>
    /// Connects <see cref="LobbyFlow"/> to the prefab panels. Buttons call the flow; each frame the visible panel is
    /// re-read and redrawn only when its data changed. One panel per <see cref="LobbyScreen"/>, swapped by CanvasGroup
    /// in the frame the screen changes; the canvas is off while the match runs (HUD and UI-03 own that screen).
    /// The join form is a modal over Home; logout confirm and server settings are modals driven by the flow flags.
    /// The endpoint picker (now in the server-settings modal) reuses <see cref="ConnectionMenuPresenter"/> as is (T-2).
    /// </summary>
    public sealed class LobbyUiPresenter : IDisposable
    {
        public const string EmptySlot = "Chỗ trống";
        public const string SelfTag = "Bạn";
        public const string ReplayWaiting = "Đang chờ người khác…";
        public const string DevHint = "Chế độ dev: vào bằng mã phòng, không dùng tài khoản";

        private readonly LobbyUiRoot _root;
        private readonly LobbyFlow _flow;
        private readonly ConnectionMenuPresenter _menu;
        private readonly Action _quit;
        private readonly Action<string> _copy;
        private readonly Func<bool> _settingsOpen;
        private readonly Action<string> _rememberName;
        private readonly UnityAction[] _unbind;
        private LobbyScreen _shown = (LobbyScreen)255;
        private int _drawnKey = int.MinValue;
        private bool _serverShown;

        public LobbyUiPresenter(LobbyUiRoot root, LobbyFlow flow, ConnectionMenuPresenter menu = null, Action quit = null,
            Action<string> copy = null, Func<bool> settingsOpen = null)
        {
            _root = root ? root : throw new ArgumentNullException(nameof(root));
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _menu = menu;
            _quit = quit ?? Quit;
            _copy = copy ?? (text => GUIUtility.systemCopyBuffer = text);
            _settingsOpen = settingsOpen;
            _unbind = BindButtons();
            BindInputs();
            FillEndpoint();
            root.Login.DevSkip.gameObject.SetActive(flow.Options.DevSkipLogin); // release builds never show it (M1)
            foreach (var panel in root.Panels) panel.Show(false);
            Tick();
        }

        public LobbyScreen Shown => _shown;
        public int CopyCount { get; private set; }

        public void Tick()
        {
            var screen = _flow.Screen;
            if (screen != _shown) SwapPanels(screen);
            if (screen == LobbyScreen.Settings && _settingsOpen != null && !_settingsOpen()) _flow.SettingsClosed();
            Redraw(screen);
            DrawModals();
        }

        public void Dispose()
        {
            foreach (var unbind in _unbind) unbind();
            var j = _root != null ? _root.JoinForm : null;
            if (j != null)
            {
                j.DisplayName.onValueChanged.RemoveAllListeners();
                j.RoomCode.onValueChanged.RemoveAllListeners();
            }
        }

        // ---- panels --------------------------------------------------------------------------------------------

        public static LobbyPanelView PanelFor(LobbyUiRoot r, LobbyScreen s)
        {
            switch (s)
            {
                case LobbyScreen.MainMenu:
                case LobbyScreen.Settings: return r.MainMenu;
                case LobbyScreen.JoinForm: return r.JoinForm;
                case LobbyScreen.Connecting: return r.Connecting;
                case LobbyScreen.Login: return r.Login;
                case LobbyScreen.RoomManager: return r.RoomManager;
                case LobbyScreen.History: return r.History;
                case LobbyScreen.Leaderboard: return r.Leaderboard;
                case LobbyScreen.Lobby: return r.Lobby;
                case LobbyScreen.Loading: return r.Loading;
                case LobbyScreen.Results: return r.Results;
                case LobbyScreen.Reconnecting: return r.Reconnect;
                case LobbyScreen.LoadError:
                case LobbyScreen.Disconnected: return r.Notice;
                default: return null; // InGame
            }
        }

        private void SwapPanels(LobbyScreen screen)
        {
            var target = PanelFor(_root, screen);
            foreach (var panel in _root.Panels) panel.Show(panel == target);
            if (screen == LobbyScreen.JoinForm) _root.MainMenu.ShowUnder(); // modal over Home, behind its scrim
            if (_root.Canvas != null) _root.Canvas.enabled = screen != LobbyScreen.InGame;
            if (_root.Backdrop != null) _root.Backdrop.SetActive(LobbyScreenRules.IsMenu(screen));
            _shown = screen;
            _drawnKey = int.MinValue;
            OnEnter(screen);
        }

        private void OnEnter(LobbyScreen screen)
        {
            switch (screen)
            {
                case LobbyScreen.MainMenu:
                    _flow.Api?.Check();
                    break;
                case LobbyScreen.JoinForm:
                    _root.JoinForm.DisplayName.SetTextWithoutNotify(_flow.Join.DisplayName);
                    _root.JoinForm.RoomCode.SetTextWithoutNotify(_flow.Join.RoomCode);
                    var user = _flow.Account?.Username?.Trim();
                    _root.JoinForm.Hint.text = _flow.DevSkipped ? DevHint
                        : string.IsNullOrEmpty(user) ? "" : "Vào bằng tài khoản " + user + " · tên hiển thị lấy từ tài khoản";
                    break;
                case LobbyScreen.Login:
                    _flow.Api?.Check(); // status line + Thử lại when the account server is down
                    _root.Login.Username.SetTextWithoutNotify(_flow.Account?.Username ?? "");
                    _root.Login.Password.SetTextWithoutNotify("");
                    break;
            }
        }

        private void Redraw(LobbyScreen screen)
        {
            switch (screen)
            {
                case LobbyScreen.MainMenu:
                case LobbyScreen.Settings: DrawMainMenu(); break;
                case LobbyScreen.JoinForm: DrawJoin(); break;
                case LobbyScreen.Connecting: DrawConnecting(); break;
                case LobbyScreen.Login: DrawLogin(); break;
                case LobbyScreen.RoomManager: DrawRooms(); break;
                case LobbyScreen.History: DrawHistory(); break;
                case LobbyScreen.Leaderboard: DrawLeaderboard(); break;
                case LobbyScreen.Lobby: DrawLobby(); break;
                case LobbyScreen.Loading: DrawLoading(); break;
                case LobbyScreen.Results: DrawResults(); break;
                case LobbyScreen.Reconnecting: DrawReconnect(); break;
                case LobbyScreen.LoadError:
                case LobbyScreen.Disconnected: DrawNotice(screen); break;
            }
        }

        private bool Changed(int key)
        {
            if (key == _drawnKey) return false;
            _drawnKey = key;
            return true;
        }

        // ---- draw ----------------------------------------------------------------------------------------------

        private void DrawMainMenu()
        {
            var v = _root.MainMenu;
            var api = _flow.Api?.Text ?? ApiMessages.NoApi;
            var dev = _flow.DevSkipped;
            var user = dev ? "Chưa đăng nhập" : _flow.Account?.Username?.Trim() ?? "";
            if (!Changed(Hash(api, user, dev ? 1 : 0))) return;
            v.ApiStatus.text = api;
            v.AccountStatus.text = dev ? "Chế độ dev" : "Đã đăng nhập";
            v.AccountName.text = user;
            SetLabel(v.Logout, dev ? "Đăng nhập" : "Đăng xuất");
            v.Join.interactable = true; // AC3: never blocked by the API
        }

        private void DrawModals()
        {
            var r = _root;
            r.LogoutConfirm.Show(_flow.LogoutConfirmOpen);
            if (_flow.LogoutConfirmOpen) r.LogoutConfirm.Text.text = "Đăng xuất khỏi " + (_flow.Account?.Username?.Trim() ?? "") + "?";
            var open = _flow.ServerSettingsOpen;
            if (open != _serverShown)
            {
                _serverShown = open;
                r.ServerSettings.Show(open);
                if (open) FillEndpointFields();
                r.ServerSettings.Audio.gameObject.SetActive(_flow.Screen == LobbyScreen.MainMenu && _flow.OpenSettingsPanel != null);
            }
            if (open) r.ServerSettings.Status.text = _menu?.Status ?? "";
        }

        private void DrawJoin()
        {
            var j = _flow.Join;
            if (!Changed(Hash(j.Message, j.NameErrorText, j.CodeErrorText, j.CanSubmit ? 1 : 0, j.TimedOut ? 1 : 0, j.FocusCode ? 1 : 0))) return;
            var v = _root.JoinForm;
            v.NameError.text = j.NameErrorText;
            v.CodeError.text = j.CodeErrorText;
            v.Message.text = j.Message;
            v.Join.interactable = j.CanSubmit;
            v.Retry.gameObject.SetActive(j.TimedOut);
            if (j.FocusCode) { v.RoomCode.Select(); v.RoomCode.ActivateInputField(); }
        }

        private void DrawConnecting()
        {
            var c = _flow.Connection;
            if (!Changed(Hash(c.Step, c.RttMs))) return;
            _root.Connecting.Step.text = "Đang kết nối (" + c.Step + ")";
            _root.Connecting.Rtt.text = c.RttMs >= 0 ? "RTT " + c.RttMs + " ms" : "";
        }

        private void DrawLogin()
        {
            var a = _flow.Account;
            var api = _flow.Api;
            var apiDown = api != null && !api.Ready && !api.Checking;
            if (a == null || !Changed(Hash(a.Message, a.RegisterMode ? 1 : 0, a.Busy ? 1 : 0, api?.Text, apiDown ? 1 : 0))) return;
            var v = _root.Login;
            v.ApiStatus.text = api == null || api.Ready ? "" : api.Text;
            v.Retry.gameObject.SetActive(apiDown);
            v.Title.text = a.RegisterMode ? "Đăng ký" : "Đăng nhập";
            v.SetTab(a.RegisterMode);
            v.Message.text = a.Message;
            v.Submit.interactable = !a.Busy;
            SetLabel(v.Submit, a.RegisterMode ? "Đăng ký" : "Đăng nhập");
        }

        private void DrawRooms()
        {
            var m = _flow.Rooms;
            if (m == null || !Changed(m.Version * 31 + (m.Creating ? 1 : 0) + (m.Deleting ? 2 : 0))) return;
            var v = _root.RoomManager;
            v.State.text = m.State == ListState.Loading ? "Đang tải…" : m.Message;
            v.Retry.gameObject.SetActive(m.State == ListState.Unavailable);
            v.Create.interactable = !m.Creating;
            for (var i = 0; i < v.Rows.Length; i++)
            {
                var row = v.Rows[i];
                var show = i < m.Rooms.Count && m.State != ListState.Loading;
                row.gameObject.SetActive(show);
                if (!show) continue;
                var room = m.Rooms[i];
                row.RoomName.text = room.Name;
                row.Occupancy.text = RoomListModel.Occupancy(room);
                row.Code.text = room.IsDefault ? "Mặc định" : room.RoomCode ?? "";
                row.Copy.gameObject.SetActive(room.RoomCode != null);
                row.Delete.gameObject.SetActive(RoomListModel.CanDelete(room));
                row.Delete.interactable = !m.Deleting;
            }
            v.ConfirmGroup.alpha = m.PendingDelete.HasValue ? 1f : 0f;
            v.ConfirmGroup.interactable = v.ConfirmGroup.blocksRaycasts = m.PendingDelete.HasValue;
            if (m.PendingDelete.HasValue) v.ConfirmText.text = "Xóa phòng này?";
        }

        private void DrawHistory()
        {
            var h = _flow.History;
            if (h == null) return;
            if (!Changed(Hash(h.State, h.Rows.Count, h.Message, h.IdentityLabel))) return;
            var v = _root.History;
            v.Identity.text = h.IdentityLabel;
            v.State.text = h.State == ListState.Loading ? "Đang tải…" : h.Message;
            v.Retry.gameObject.SetActive(h.State == ListState.Unavailable);
            for (var i = 0; i < v.Rows.Length; i++)
            {
                var show = i < h.Rows.Count;
                v.Rows[i].gameObject.SetActive(show);
                if (show) v.Rows[i].Line.text = HistoryModel.Line(h.Rows[i]);
            }
        }

        private void DrawLeaderboard()
        {
            var b = _flow.Leaderboard;
            if (b == null || !Changed(Hash(b.State, b.Rows.Length, b.Message, b.MatchId))) return;
            var v = _root.Leaderboard;
            v.State.text = b.State == ListState.Loading ? "Đang tải…" : b.Message;
            v.Retry.gameObject.SetActive(b.State == ListState.Unavailable);
            for (var i = 0; i < v.Rows.Length; i++)
            {
                var show = i < b.Rows.Length;
                var row = v.Rows[i];
                row.gameObject.SetActive(show);
                if (!show) continue;
                var e = b.Rows[i];
                row.Rank.text = "#" + e.Rank;
                row.PlayerName.text = e.Name;
                row.Kills.gameObject.SetActive(_flow.Options.ShowKills);
                row.Kills.text = e.Kills.ToString(CultureInfo.InvariantCulture);
                row.Survival.text = ResultRow.Clock((float)e.SurvivalSeconds);
                row.Background.enabled = false;
            }
        }

        private void DrawLobby()
        {
            var s = _flow.Lobby;
            var slots = s.Slots;
            var key = Hash(s.MatchId, slots.Count, s.MaxPlayers, s.MinPlayers, _flow.HasCountdown ? _flow.CountdownSeconds : -1,
                _flow.Notice, _flow.ReadyRequests, s.RoomCode);
            for (var i = 0; i < slots.Count; i++) key = key * 31 + slots[i].NetId * 7 + (slots[i].IsSelf ? 1 : 0) + (slots[i].Ready ? 2 : 0);
            if (!Changed(key)) return;
            var v = _root.Lobby;
            var o = _flow.Options;
            v.RoomCode.text = s.RoomCode;
            v.Copy.gameObject.SetActive(!string.IsNullOrEmpty(s.RoomCode));
            var max = Mathf.Clamp(s.MaxPlayers, 1, v.Slots.Length);
            for (var i = 0; i < v.Slots.Length; i++)
            {
                var row = v.Slots[i];
                row.gameObject.SetActive(i < max);
                if (i >= max) continue;
                if (i < slots.Count)
                {
                    var slot = slots[i];
                    row.PlayerName.text = slot.Name;
                    row.Badge.text = slot.IsSelf ? SelfTag : o.ReadyEnabled && slot.HasReady && slot.Ready ? "Sẵn sàng" : "";
                    row.Tint(true, slot.IsSelf);
                }
                else
                {
                    row.PlayerName.text = EmptySlot;
                    row.Badge.text = "";
                    row.Tint(false, false);
                }
            }
            var count = s.PlayerCount > 0 ? s.PlayerCount : slots.Count;
            v.Count.text = count + "/" + s.MaxPlayers + " · tối thiểu " + s.MinPlayers;
            v.Status.text = _flow.LobbyStatusText;
            v.Rule.text = o.ReadyEnabled ? "" : _flow.AutoStartText;
            v.Notice.text = _flow.Notice;
            v.Ready.gameObject.SetActive(o.ReadyEnabled);
            v.Ready.interactable = _flow.ReadyRequests == 0;
            v.ReadyLabel.text = _flow.ReadyRequests > 0 ? "Đã sẵn sàng" : "Sẵn sàng";
            v.Start.gameObject.SetActive(o.StartEnabled);
            v.Start.interactable = false; // T-6: the server has no start command
        }

        private void DrawLoading()
        {
            var seconds = _flow.HasCountdown ? _flow.CountdownSeconds : -1;
            if (!Changed(Hash(seconds, _flow.Lobby.MatchId))) return;
            var v = _root.Loading;
            v.StepMap.text = "1. Tải dữ liệu bản đồ";
            v.StepVersion.text = "2. Kiểm tra phiên bản";
            v.StepPlayers.text = "3. Chờ người chơi";
            v.TimeLeft.text = seconds >= 0 ? "Còn " + seconds + " s" : "";
            v.Hint.text = _flow.Options.ControlsHint;
            var tips = _flow.Options.Tips;
            v.Tip.text = tips != null && tips.Length > 0 ? "Mẹo: " + tips[(int)((uint)_flow.Lobby.MatchId.GetHashCode() % tips.Length)] : "";
        }

        private void DrawResults()
        {
            var r = _flow.Result;
            var apiReady = _flow.Api != null && _flow.Api.Ready;
            var ending = _flow.Lobby.Phase == MatchPhase.Ending;
            if (!Changed(Hash(r.MatchId, r.HasData ? 1 : 0, _flow.ReplayPending ? 1 : 0, apiReady ? 1 : 0, ending ? 1 : 0))) return;
            var v = _root.Results;
            v.Title.text = r.HasData ? r.Title : "Đang chờ kết quả…";
            SetOptional(v.Rank, r.RankText);
            SetOptional(v.Kills, r.KillsText);
            SetOptional(v.Survival, r.SurvivalText);
            SetOptional(v.Cause, r.CauseText);
            v.OnlineLine.text = apiReady ? "" : ApiMessages.HistoryUnavailableLine;
            v.Online.gameObject.SetActive(apiReady && r.HasData);
            v.Replay.interactable = ending && !_flow.ReplayPending;
            v.ReplayLabel.text = _flow.ReplayPending ? ReplayWaiting : "Chơi lại";
            for (var i = 0; i < v.Rows.Length; i++)
            {
                var row = v.Rows[i];
                var show = i < r.Rows.Count;
                row.gameObject.SetActive(show);
                if (!show) continue;
                var e = r.Rows[i];
                row.Rank.text = "#" + e.Rank;
                row.PlayerName.text = e.Name;
                row.Kills.gameObject.SetActive(_flow.Options.ShowKills);
                row.Kills.text = e.Kills.ToString(CultureInfo.InvariantCulture);
                row.Survival.text = ResultRow.Clock(e.SurvivalSeconds);
                row.Background.enabled = e.IsSelf;
            }
        }

        private void DrawReconnect()
        {
            var left = _flow.Reconnect.SecondsLeft;
            if (!Changed(left)) return;
            _root.Reconnect.Message.text = "Đang kết nối lại…";
            _root.Reconnect.Seconds.text = "Còn khoảng " + left + " s";
        }

        private void DrawNotice(LobbyScreen screen)
        {
            var text = screen == LobbyScreen.LoadError ? _flow.LoadErrorText : _flow.Notice;
            if (!Changed(Hash((int)screen, text))) return;
            _root.Notice.Title.text = screen == LobbyScreen.LoadError ? "Không vào được trận" : "Mất kết nối";
            _root.Notice.Message.text = text;
        }

        // ---- input ---------------------------------------------------------------------------------------------

        private UnityAction[] BindButtons()
        {
            var r = _root;
            var f = _flow;
            var list = new System.Collections.Generic.List<UnityAction>();
            void On(Button b, UnityAction a)
            {
                if (b == null) return;
                b.onClick.AddListener(a);
                list.Add(() => b.onClick.RemoveListener(a));
            }

            On(r.MainMenu.Join, f.OpenJoin);
            On(r.MainMenu.Rooms, f.OpenRooms);
            On(r.MainMenu.History, f.OpenHistory);
            On(r.MainMenu.Leaderboard, f.OpenLatestLeaderboard);
            On(r.MainMenu.Settings, f.OpenServerSettings);
            On(r.MainMenu.Quit, () => _quit());
            On(r.MainMenu.Logout, f.RequestLogout);
            On(r.LogoutConfirm.Yes, f.ConfirmLogout);
            On(r.LogoutConfirm.No, f.CancelLogout);
            On(r.ServerSettings.Save, SaveEndpoint);
            On(r.ServerSettings.Audio, f.OpenSettings);
            On(r.ServerSettings.Close, f.CloseServerSettings);
            On(r.JoinForm.Join, () => f.SubmitJoin());
            On(r.JoinForm.Retry, () => f.SubmitJoin());
            On(r.JoinForm.Back, f.Back);
            On(r.Connecting.Cancel, f.CancelJoin);
            On(r.Login.LoginTab, () => { if (f.Account != null) f.Account.RegisterMode = false; _drawnKey = int.MinValue; });
            On(r.Login.RegisterTab, () => { if (f.Account != null) f.Account.RegisterMode = true; _drawnKey = int.MinValue; });
            On(r.Login.Submit, SubmitLogin);
            On(r.Login.Advanced, f.OpenServerSettings);
            On(r.Login.Retry, () => f.Api?.Check());
            On(r.Login.DevSkip, f.DevSkipLogin);
            On(r.RoomManager.Create, CreateRoom);
            On(r.RoomManager.Retry, () => f.Rooms?.Refresh());
            On(r.RoomManager.Back, f.Back);
            On(r.RoomManager.ConfirmYes, () => f.Rooms?.ConfirmDelete());
            On(r.RoomManager.ConfirmNo, () => f.Rooms?.CancelDelete());
            for (var i = 0; i < r.RoomManager.Rows.Length; i++)
            {
                var index = i;
                var row = r.RoomManager.Rows[i];
                On(row.Copy, () => { if (TryRoom(index, out var room) && room.RoomCode != null) Copy(room.RoomCode); });
                On(row.Join, () => { if (TryRoom(index, out var room)) f.JoinRoom(room); });
                On(row.Delete, () => { if (TryRoom(index, out var room)) f.Rooms.RequestDelete(room.Id); });
            }
            On(r.Lobby.Copy, () => Copy(f.Lobby.RoomCode));
            On(r.Lobby.Ready, () => f.Ready());
            On(r.Lobby.Leave, f.GoHome);
            On(r.Loading.Home, f.GoHome);
            On(r.Results.Replay, () => f.Replay());
            On(r.Results.Home, f.GoHome);
            On(r.Results.Online, () => f.OpenLeaderboard(f.Result.MatchId));
            On(r.History.Retry, () => { _ = f.History?.Refresh(); });
            On(r.History.Back, f.Back);
            for (var i = 0; i < r.History.Rows.Length; i++)
            {
                var index = i;
                On(r.History.Rows[i].Open, () =>
                {
                    if (f.History != null && index < f.History.Rows.Count) f.OpenLeaderboard(f.History.Rows[index].MatchId);
                });
            }
            On(r.Leaderboard.Retry, () => f.Leaderboard?.Load(f.Leaderboard.MatchId));
            On(r.Leaderboard.Back, f.Back);
            On(r.Reconnect.Home, f.GoHome);
            On(r.Notice.Home, f.GoHome);
            return list.ToArray();
        }

        private void BindInputs()
        {
            var j = _root.JoinForm;
            j.DisplayName.onValueChanged.AddListener(_flow.Join.SetName);
            j.RoomCode.onValueChanged.AddListener(raw =>
            {
                _flow.Join.SetCode(raw);
                if (raw != _flow.Join.RoomCode) j.RoomCode.SetTextWithoutNotify(_flow.Join.RoomCode); // AC5: upper-case
            });
            var l = _root.Login;
            if (_flow.Account != null)
            {
                l.Username.onValueChanged.AddListener(t => _flow.Account.Username = t);
                l.Password.onValueChanged.AddListener(t => _flow.Account.Password = t);
            }
            if (_menu != null) _root.ServerSettings.Profile.onValueChanged.AddListener(SelectProfile);
        }

        private void SubmitLogin()
        {
            _flow.Account?.Submit();
            _root.Login.Password.SetTextWithoutNotify(""); // AC13: cleared even when the call fails
            _drawnKey = int.MinValue;
        }

        private void CreateRoom()
        {
            var rooms = _flow.Rooms;
            if (rooms == null) return;
            rooms.NewName = _root.RoomManager.NewName.text;
            rooms.NewCapacity = int.TryParse(_root.RoomManager.NewCapacity.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                ? n : LobbyLimits.PlayersPerRoom;
            rooms.Create();
        }

        private bool TryRoom(int index, out RoomInfo room)
        {
            var rooms = _flow.Rooms;
            room = default;
            if (rooms == null || index >= rooms.Rooms.Count) return false;
            room = rooms.Rooms[index];
            return true;
        }

        private void Copy(string code)
        {
            if (string.IsNullOrEmpty(code)) return;
            CopyCount++;
            _copy(code.Length > RoomCodeRules.Length ? code.Substring(0, RoomCodeRules.Length) : code); // G-9: 6 chars
        }

        private void FillEndpoint()
        {
            var m = _root.ServerSettings;
            if (_menu == null)
            {
                m.Profile.gameObject.SetActive(false);
                return;
            }
            m.Profile.ClearOptions();
            m.Profile.AddOptions(new System.Collections.Generic.List<string>(_menu.Profiles));
            var current = System.Array.FindIndex(_menu.Profiles, name => _menu.ConfigSource == "profile:" + name);
            if (current < 0) // a saved client.json or command line: name it instead of showing the first profile
            {
                m.Profile.AddOptions(new System.Collections.Generic.List<string> { "Đang dùng: " + _menu.ConfigSource });
                current = m.Profile.options.Count - 1;
            }
            m.Profile.SetValueWithoutNotify(current); // QA m-a: show the profile in use, not the first one
            m.Host.SetTextWithoutNotify(_menu.Host ?? "");
            m.Port.SetTextWithoutNotify(_menu.Port ?? "");
            m.Api.SetTextWithoutNotify(_menu.ApiBaseUrl ?? "");
        }

        private void SelectProfile(int index)
        {
            var profiles = _menu.Profiles;
            if (index < 0 || index >= profiles.Length) return;
            _menu.SelectProfile(profiles[index]);
            FillEndpointFields();
        }

        private void SaveEndpoint()
        {
            if (_menu == null) return;
            var m = _root.ServerSettings;
            _menu.Host = m.Host.text;
            _menu.Port = m.Port.text;
            _menu.ApiBaseUrl = m.Api.text;
            _menu.SaveEndpoint();
            _flow.Api?.Check();
        }

        private void FillEndpointFields()
        {
            if (_menu == null) return;
            var m = _root.ServerSettings;
            m.Host.SetTextWithoutNotify(_menu.Host ?? "");
            m.Port.SetTextWithoutNotify(_menu.Port ?? "");
            m.Api.SetTextWithoutNotify(_menu.ApiBaseUrl ?? "");
            _flow.Api?.Check();
        }

        private static void SetOptional(TMP_Text text, string value)
        {
            text.gameObject.SetActive(value != null);
            if (value != null) text.text = value;
        }

        private static void SetLabel(Button button, string text)
        {
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = text;
        }

        private static int Hash(params object[] parts)
        {
            unchecked
            {
                var h = 17;
                foreach (var p in parts) h = h * 31 + (p?.GetHashCode() ?? 0);
                return h;
            }
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
