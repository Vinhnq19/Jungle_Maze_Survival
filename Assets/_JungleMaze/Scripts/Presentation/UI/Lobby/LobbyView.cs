using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Lobby
{
    /// <summary>Lobby (Figma v2 42:182): room code + copy, 10 slots, n/max, minimum, server countdown, ready/start behind flags (AC20–AC23). References only.</summary>
    public sealed class LobbyView : LobbyPanelView
    {
        [SerializeField] private TMP_Text roomCode;
        [SerializeField] private Button copy;
        [SerializeField] private TMP_Text count;
        [SerializeField] private TMP_Text status;
        [SerializeField] private TMP_Text rule;
        [SerializeField] private TMP_Text notice;
        [SerializeField] private Button ready;
        [SerializeField] private TMP_Text readyLabel;
        [SerializeField] private Button start;
        [SerializeField] private Button leave;
        [SerializeField] private LobbySlotView[] slots;

        public TMP_Text RoomCode => roomCode;
        public Button Copy => copy;
        public TMP_Text Count => count;
        public TMP_Text Status => status;
        public TMP_Text Rule => rule;
        public TMP_Text Notice => notice;
        public Button Ready => ready;
        public TMP_Text ReadyLabel => readyLabel;
        public Button Start => start;
        public Button Leave => leave;
        public LobbySlotView[] Slots => slots;
    }
}
