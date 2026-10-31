using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Lobby
{
    /// <summary>Guest join form (Figma v2 42:137): name, 6-char room code, inline errors, join/retry/back (AC5–AC10). References only.</summary>
    public sealed class JoinFormView : LobbyPanelView
    {
        [SerializeField] private TMP_InputField displayName;
        [SerializeField] private TMP_InputField roomCode;
        [SerializeField] private TMP_Text nameError;
        [SerializeField] private TMP_Text codeError;
        [SerializeField] private TMP_Text message;
        [SerializeField] private Button join;
        [SerializeField] private Button retry;
        [SerializeField] private Button back;

        public TMP_InputField DisplayName => displayName;
        public TMP_InputField RoomCode => roomCode;
        public TMP_Text NameError => nameError;
        public TMP_Text CodeError => codeError;
        public TMP_Text Message => message;
        public Button Join => join;
        public Button Retry => retry;
        public Button Back => back;
    }
}
