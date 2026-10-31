using System;
using System.Collections.Generic;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.Domain.Lobby;

namespace JungleMaze.App.Lobby
{
    /// <summary>
    /// Results screen data (AC28–AC30). The headline comes only from <c>Reason</c> and <c>WinnerNetId</c>; ranks are the
    /// server's (ties keep the same number); the own row is found by NetId, never by name. Missing data stays null
    /// so the view hides the line instead of printing "0".
    /// </summary>
    public sealed class ResultModel
    {
        public const string VictoryText = "CHIẾN THẮNG";
        public const string TieText = "HÒA, không có người thắng";
        public const string CancelledText = "Trận bị hủy";

        private readonly List<ResultRow> _rows = new List<ResultRow>();

        public Guid MatchId { get; private set; }
        public bool HasData { get; private set; }
        public ResultHeadline Headline { get; private set; }
        public string Title { get; private set; } = "";
        /// <summary>"#n / N"; null when the own row is missing.</summary>
        public string RankText { get; private set; }
        public string KillsText { get; private set; }
        public string SurvivalText { get; private set; }
        /// <summary>Elimination cause from UI-03 (N8); null hides the line.</summary>
        public string CauseText { get; private set; }
        public IReadOnlyList<ResultRow> Rows => _rows;

        public void Show(MatchResultView result, string cause, bool showKills)
        {
            Clear();
            if (result == null) return;
            HasData = true;
            MatchId = result.MatchId;
            var participants = result.Participants ?? Array.Empty<MatchParticipantState>();
            var count = Math.Min(participants.Length, Math.Max((int)result.ParticipantCount, participants.Length));
            ResultRow? self = null;
            for (var i = 0; i < count; i++)
            {
                var p = participants[i];
                var row = new ResultRow(p.NetId, p.Rank, p.Kills, p.SurvivalTicks / (float)LobbyCountdown.TickRate,
                    p.DisplayName, p.NetId == result.OwnNetId);
                _rows.Add(row);
                if (row.IsSelf) self = row;
            }
            // Stable sort by the server's rank; equal ranks keep packet order (AC30).
            for (var i = 1; i < _rows.Count; i++)
            {
                var r = _rows[i];
                var j = i - 1;
                while (j >= 0 && _rows[j].Rank > r.Rank) { _rows[j + 1] = _rows[j]; j--; }
                _rows[j + 1] = r;
            }

            Headline = HeadlineFor(result);
            var n = result.ParticipantCount > 0 ? result.ParticipantCount : count;
            Title = Headline == ResultHeadline.Victory ? VictoryText
                : Headline == ResultHeadline.Tie ? TieText
                : Headline == ResultHeadline.Cancelled ? CancelledText
                : self.HasValue && self.Value.Rank > 0 ? "HẠNG #" + self.Value.Rank : "KẾT QUẢ";
            if (self.HasValue)
            {
                if (self.Value.Rank > 0) RankText = "#" + self.Value.Rank + " / " + n;
                if (showKills) KillsText = "Hạ gục: " + self.Value.Kills;
                SurvivalText = "Sống sót: " + ResultRow.Clock(self.Value.SurvivalSeconds);
            }
            CauseText = string.IsNullOrEmpty(cause) ? null : cause;
        }

        public static ResultHeadline HeadlineFor(MatchResultView r)
        {
            if (r.Reason == MatchEndReason.Tie) return ResultHeadline.Tie;
            if (r.Reason == MatchEndReason.Cancelled) return ResultHeadline.Cancelled;
            return r.Reason == MatchEndReason.LastAlive && r.WinnerNetId == r.OwnNetId && r.OwnNetId != 255
                ? ResultHeadline.Victory : ResultHeadline.Ranked;
        }

        public void Clear()
        {
            _rows.Clear();
            HasData = false;
            MatchId = Guid.Empty;
            Title = "";
            RankText = KillsText = SurvivalText = CauseText = null;
        }
    }
}
