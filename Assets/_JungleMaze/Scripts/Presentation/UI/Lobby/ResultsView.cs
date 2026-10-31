using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Lobby
{
    /// <summary>Results (Figma v2 43:445): headline, own block, ranked table, replay/home, optional online board (AC28–AC33). References only.</summary>
    public sealed class ResultsView : LobbyPanelView
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text rank;
        [SerializeField] private TMP_Text kills;
        [SerializeField] private TMP_Text survival;
        [SerializeField] private TMP_Text cause;
        [SerializeField] private TMP_Text onlineLine;
        [SerializeField] private Button replay;
        [SerializeField] private TMP_Text replayLabel;
        [SerializeField] private Button home;
        [SerializeField] private Button online;
        [SerializeField] private ResultRowView[] rows;

        public TMP_Text Title => title;
        public TMP_Text Rank => rank;
        public TMP_Text Kills => kills;
        public TMP_Text Survival => survival;
        public TMP_Text Cause => cause;
        public TMP_Text OnlineLine => onlineLine;
        public Button Replay => replay;
        public TMP_Text ReplayLabel => replayLabel;
        public Button Home => home;
        public Button Online => online;
        public ResultRowView[] Rows => rows;
    }
}
