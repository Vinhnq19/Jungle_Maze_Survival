using _Codebase_.Entity.Player;
using Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Codebase_.Camera
{
    public class PlayerCamUI : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text info;

        [SerializeField]
        private TMP_Text kda;

        [SerializeField]
        private Image bg;

        [SerializeField]
        private Transform reviveTimeTransform;

        [SerializeField]
        private TextMeshProUGUI reviveTimeText;

        private bool _selected;
        public PlayerEntity PlayerInfo { get; set; }

        public bool Selected
        {
            get => this._selected;
            set
            {
                if (value) CameraManager.I.DeselectAll();
                this._selected = value;
                this.bg.color = this._selected ? Color.yellow : Color.white;
                CameraController.I.UpdateCams();
            }
        }

        public void ChangeInfo()
        {
            this.info.text = PlayerEntity.FormatName(this.PlayerInfo.Name, 7);
            this.kda.text = this.PlayerInfo.Score + " - " + this.PlayerInfo.Kill + " - " + this.PlayerInfo.Dead;

            if (this.PlayerInfo.IsDead)
            {
                this.reviveTimeTransform.gameObject.SetActive(true);

                this.reviveTimeText.text = this.PlayerInfo.IsOut
                    ? "OUT"
                    : Mathf.FloorToInt(this.PlayerInfo.ReviveTimeCounter).ToString();
            }
            else
            {
                this.reviveTimeTransform.gameObject.SetActive(false);
            }
        }

        public void OnClick()
        {
            this.Selected = !this.Selected;
        }
    }
}