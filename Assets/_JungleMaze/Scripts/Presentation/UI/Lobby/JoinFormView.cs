using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Lobby
{
    /// <summary>
    /// Join modal over Home (Figma 06_join-modal 121:106): name prefilled from the account, 6-char room code, inline
    /// errors, join/retry/Hủy and the "Vào bằng tài khoản …" hint (AC5–AC10). References only.
    /// </summary>
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
        [SerializeField] private TMP_Text hint;

        public TMP_InputField DisplayName => displayName;
        public TMP_InputField RoomCode => roomCode;
        public TMP_Text NameError => nameError;
        public TMP_Text CodeError => codeError;
        public TMP_Text Message => message;
        public Button Join => join;
        public Button Retry => retry;
        public Button Back => back;
        public TMP_Text Hint => hint;
    }
}
