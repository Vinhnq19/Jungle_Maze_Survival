using System;
using System.Collections.Generic;
using System.Linq;
using Managers;
using Screen.Components;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Screen
{

    public class LobbyScreen : BaseScreen
    {

        public TMP_InputField mapTimeInput;

        [SerializeField]
        private TMP_Text gameIDText;

        [field: SerializeField]
        public Button PlayBtn { get; private set; }

        [SerializeField]
        private List<Button> mapSelectionButtons;

        [SerializeField]
        private Transform slotContainer;

        [SerializeField]
        private TMP_Text[] textTeams;

        [SerializeField]
        private Sprite nonPlayerSlot;

        [SerializeField]
        private Sprite hasPlayerSlot;

        [SerializeField]
        private TMP_Text connectedText;

        public List<Slot> Slots { get; private set; }

        public Sprite NonPlayerSlot => this.nonPlayerSlot;
        public Sprite HasPlayerSlot => this.hasPlayerSlot;

        private void Awake()
        {
            this.Slots = new();

            foreach (Transform slot in this.slotContainer)
            {
                slot.GetComponentInChildren<Image>().sprite = this.nonPlayerSlot;
                this.Slots.Add(slot.GetComponent<Slot>());
            }
        }

        private void Update()
        {
            this.connectedText.text = NetworkManager.I.IsConnected ? "Connected" : "Not connected";

            if (GameManager.I.CurrentGameId != null)
                this.gameIDText.text = "Game Id: " + GameManager.I.CurrentGameId;

            int.TryParse(this.mapTimeInput.text, out var mapTime);
            MapManager.I.MapTime = mapTime;
        }

        private void OnEnable()
        {
            this.connectedText.text = "Not connected";
        }

        public void SetTeamNames(IList<string> names)
        {
            for (var i = 0; i < this.textTeams.Length; i++)
            {
                if (i >= names.Count)
                {
                    this.textTeams[i].gameObject.SetActive(false);
                    continue;
                }

                this.textTeams[i].gameObject.SetActive(true);
                this.textTeams[i].text = names[i];
            }
        }

        public override void Show()
        {
            this.SetTeamNames(new List<string>());
            base.Show();
        }

        public override void Hide()
        {
            this.SetTeamNames(new List<string>());
            base.Hide();
        }

        public void SetOnClickMapButton(Action<int> onClickMapButton)
        {
            foreach (var (button, index) in this.mapSelectionButtons.Select((a, b) => (a, b)))
            {
                button.onClick.RemoveAllListeners();

                button.onClick.AddListener(() =>
                {
                    onClickMapButton?.Invoke(index);
                });
            }
        }
    }
}