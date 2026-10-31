using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Lobby
{
    /// <summary>Room manager (v1 8:124 layout): list with states, create form, one-step delete confirm (AC14–AC16); logout moved to the Home account card. References only.</summary>
    public sealed class RoomManagerView : LobbyPanelView
    {
        [SerializeField] private TMP_Text state;
        [SerializeField] private TMP_InputField newName;
        [SerializeField] private TMP_InputField newCapacity;
        [SerializeField] private Button create;
        [SerializeField] private Button retry;
        [SerializeField] private Button back;
        [SerializeField] private CanvasGroup confirmGroup;
        [SerializeField] private TMP_Text confirmText;
        [SerializeField] private Button confirmYes;
        [SerializeField] private Button confirmNo;
        [SerializeField] private RoomRowView[] rows;

        public TMP_Text State => state;
        public TMP_InputField NewName => newName;
        public TMP_InputField NewCapacity => newCapacity;
        public Button Create => create;
        public Button Retry => retry;
        public Button Back => back;
        public CanvasGroup ConfirmGroup => confirmGroup;
        public TMP_Text ConfirmText => confirmText;
        public Button ConfirmYes => confirmYes;
        public Button ConfirmNo => confirmNo;
        public RoomRowView[] Rows => rows;
    }
}
