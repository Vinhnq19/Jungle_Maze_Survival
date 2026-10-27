using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.MatchOverlay
{
    /// <summary>
    /// The ESC panel (G-10: 560 px, centred). Only references; colors, sprites and fonts live on the prefab (AC47).
    /// Volume sliders run 0–1 and show %, the sensitivity slider runs 5–20 whole steps (= 0.5–2.0, G-9).
    /// </summary>
    public sealed class MatchMenuView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Slider master;
        [SerializeField] private Slider sfx;
        [SerializeField] private Slider music;
        [SerializeField] private Slider sensitivity;
        [SerializeField] private TMP_Text masterValue;
        [SerializeField] private TMP_Text sfxValue;
        [SerializeField] private TMP_Text musicValue;
        [SerializeField] private TMP_Text sensitivityValue;
        [SerializeField] private Toggle musicEnabled;
        [SerializeField] private Toggle screenShake;
        [Tooltip("Shown next to the Music slider while background music is off (Q9).")]
        [SerializeField] private TMP_Text musicOffLabel;
        [SerializeField] private Button resume;
        [SerializeField] private Button defaults;
        [SerializeField] private Button leave;
        [Tooltip("G-7: \"Chưa thoát được, thử lại\".")]
        [SerializeField] private TMP_Text retryNotice;

        public CanvasGroup Group => group;
        public Slider Master => master;
        public Slider Sfx => sfx;
        public Slider Music => music;
        public Slider Sensitivity => sensitivity;
        public TMP_Text MasterValue => masterValue;
        public TMP_Text SfxValue => sfxValue;
        public TMP_Text MusicValue => musicValue;
        public TMP_Text SensitivityValue => sensitivityValue;
        public Toggle MusicEnabled => musicEnabled;
        public Toggle ScreenShake => screenShake;
        public TMP_Text MusicOffLabel => musicOffLabel;
        public Button Resume => resume;
        public Button Defaults => defaults;
        public Button Leave => leave;
        public TMP_Text RetryNotice => retryNotice;
    }
}
