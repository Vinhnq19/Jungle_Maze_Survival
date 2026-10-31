using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.Lobby
{
    /// <summary>Loading (Figma v2 42:269): steps, server time left, controls hint and a tip (AC24). References only.</summary>
    public sealed class LoadingView : LobbyPanelView
    {
        [SerializeField] private TMP_Text stepMap;
        [SerializeField] private TMP_Text stepVersion;
        [SerializeField] private TMP_Text stepPlayers;
        [SerializeField] private TMP_Text timeLeft;
        [SerializeField] private TMP_Text hint;
        [SerializeField] private TMP_Text tip;
        [SerializeField] private Button home;

        public TMP_Text StepMap => stepMap;
        public TMP_Text StepVersion => stepVersion;
        public TMP_Text StepPlayers => stepPlayers;
        public TMP_Text TimeLeft => timeLeft;
        public TMP_Text Hint => hint;
        public TMP_Text Tip => tip;
        public Button Home => home;
    }
}
