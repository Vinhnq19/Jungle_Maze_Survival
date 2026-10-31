using System;
using System.Collections.Generic;
using JungleMaze.Domain.Lobby;

namespace JungleMaze.App.Lobby
{
    public enum ListState : byte { Idle, Loading, Ready, Empty, Unavailable }

    /// <summary>
    /// Room manager (AC14–AC17): list, create (one request per press), delete with one confirmation. A 401 from any
    /// call expires the session and raises <see cref="AuthExpired"/>; nothing is sent without a token.
    /// </summary>
    public sealed class RoomListModel
    {
        private readonly IRoomsApi _api;
        private readonly AccountSession _account;
        private readonly List<RoomInfo> _rooms = new List<RoomInfo>();

        public RoomListModel(IRoomsApi api, AccountSession account)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _account = account ?? throw new ArgumentNullException(nameof(account));
        }

        public ListState State { get; private set; }
        public IReadOnlyList<RoomInfo> Rooms => _rooms;
        public string Message { get; private set; } = "";
        public bool Creating { get; private set; }
        public bool Deleting { get; private set; }
        public Guid? PendingDelete { get; private set; }
        public int Version { get; private set; }

        public string NewName = "";
        public int NewCapacity = LobbyLimits.PlayersPerRoom;

        public event Action AuthExpired;

        public static bool CanDelete(in RoomInfo room) => !room.IsDefault;

        public static string Occupancy(in RoomInfo room) => room.Active + room.Pending + "/" + room.MaxPlayers;

        public void Refresh()
        {
            if (State == ListState.Loading) return;
            var token = TokenOrExpire();
            if (token == null) return;
            State = ListState.Loading;
            Message = "";
            Changed();
            _api.List(token, r =>
            {
                if (Unauthorized(r.HttpStatus)) return;
                if (!r.Ok) { State = ListState.Unavailable; Message = ApiMessages.For(ApiOp.ListRooms, r); Changed(); return; }
                _rooms.Clear();
                if (r.Value != null) _rooms.AddRange(r.Value);
                SetListState();
                Changed();
            });
        }

        public string ValidateNew()
        {
            var name = (NewName ?? "").Trim();
            if (name.Length < 1 || name.Length > LobbyLimits.RoomNameMax) return "Tên phòng cần 1–48 ký tự";
            return NewCapacity < 1 || NewCapacity > LobbyLimits.PlayersPerRoom ? "Sức chứa 1–10 người" : null;
        }

        public bool Create()
        {
            if (Creating) return false;
            var invalid = ValidateNew();
            if (invalid != null) { Message = invalid; Changed(); return false; }
            var token = TokenOrExpire();
            if (token == null) return false;
            Creating = true;
            Message = "";
            _api.Create(token, NewName.Trim(), NewCapacity, r =>
            {
                Creating = false;
                if (Unauthorized(r.HttpStatus)) return;
                if (!r.Ok) { Message = ApiMessages.For(ApiOp.CreateRoom, r); Changed(); return; }
                _rooms.Add(r.Value);
                NewName = "";
                SetListState();
                Changed();
            });
            return true;
        }

        /// <summary>First press on "Xóa" asks (AC16); the default room has no delete.</summary>
        public void RequestDelete(Guid id)
        {
            var i = IndexOf(id);
            if (i < 0 || !CanDelete(_rooms[i])) return;
            PendingDelete = id;
            Changed();
        }

        public void CancelDelete()
        {
            PendingDelete = null;
            Changed();
        }

        public bool ConfirmDelete()
        {
            if (Deleting || PendingDelete == null) return false;
            var token = TokenOrExpire();
            if (token == null) return false;
            var id = PendingDelete.Value;
            PendingDelete = null;
            Deleting = true;
            Message = "";
            _api.Delete(token, id, r =>
            {
                Deleting = false;
                if (Unauthorized(r.HttpStatus)) return;
                if (r.Ok || r.HttpStatus == 404) RemoveRoom(id); // 404: the room is already gone
                if (!r.Ok) Message = ApiMessages.For(ApiOp.DeleteRoom, r);
                SetListState();
                Changed();
            });
            return true;
        }

        public void Clear()
        {
            _rooms.Clear();
            State = ListState.Idle;
            Message = "";
            PendingDelete = null;
            Changed();
        }

        private string TokenOrExpire()
        {
            var token = _account.Token;
            if (token != null) return token;
            _account.Expire();
            AuthExpired?.Invoke();
            return null;
        }

        private bool Unauthorized(int status)
        {
            if (status != 401) return false;
            if (State == ListState.Loading) State = ListState.Idle;
            _account.Expire();
            AuthExpired?.Invoke();
            Changed();
            return true;
        }

        private void SetListState()
        {
            var own = 0;
            foreach (var room in _rooms) if (!room.IsDefault) own++;
            State = own == 0 ? ListState.Empty : ListState.Ready;
            if (State == ListState.Empty && Message.Length == 0) Message = ApiMessages.NoRooms;
            else if (State == ListState.Ready && Message == ApiMessages.NoRooms) Message = "";
        }

        private void RemoveRoom(Guid id)
        {
            var i = IndexOf(id);
            if (i >= 0) _rooms.RemoveAt(i);
        }

        private int IndexOf(Guid id)
        {
            for (var i = 0; i < _rooms.Count; i++) if (_rooms[i].Id == id) return i;
            return -1;
        }

        private void Changed() => Version++;
    }
}
