using JungleMaze.Domain.MatchUi;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JungleMaze.Presentation.UI.MatchOverlay
{
    /// <summary>
    /// Spectator overlay (Figma v1 7:304, AC46): "Đang theo dõi" block (430,24) 420×71, key bar (440,640) h44,
    /// "Về sảnh" (1114,640) 100×47, target card (24,560) 300×90, list from (24,130) with 9 pre-built rows. The card shows
    /// only what the state gives: name, HP if any, weapon if any; never ammo or backpack (Q12).
    /// </summary>
    public sealed class SpectatorView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform watchingBlock;
        [SerializeField] private TMP_Text watchingName;
        [SerializeField] private TMP_Text aliveCount;
        [SerializeField] private RectTransform keyBar;
        [SerializeField] private Button killer;
        [SerializeField] private Button home;
        [SerializeField] private RectTransform card;
        [SerializeField] private TMP_Text cardName;
        [SerializeField] private GameObject cardHp;
        [SerializeField] private RectTransform cardHpFill;
        [SerializeField] private TMP_Text cardHpText;
        [SerializeField] private TMP_Text cardWeapon;
        [SerializeField] private SpectatorRowView[] rows = new SpectatorRowView[0];

        private int _shownAlive = -1, _shownStart = -1, _shownHp = -1, _shownMaxHp = -1;
        private string _shownName, _shownWeapon;

        public CanvasGroup Group => group;
        public RectTransform WatchingBlock => watchingBlock;
        public TMP_Text WatchingName => watchingName;
        public TMP_Text AliveCount => aliveCount;
        public RectTransform KeyBar => keyBar;
        public Button Killer => killer;
        public Button Home => home;
        public RectTransform Card => card;
        public TMP_Text CardName => cardName;
        public GameObject CardHp => cardHp;
        public TMP_Text CardWeapon => cardWeapon;
        public SpectatorRowView[] Rows => rows;

        /// <summary>"Còn sống n/N"; rebuilt only when a number changes.</summary>
        public void SetAlive(int alive, int start)
        {
            if (alive == _shownAlive && start == _shownStart) return;
            _shownAlive = alive;
            _shownStart = start;
            aliveCount.text = start > 0 ? "Còn sống " + alive + "/" + start : "Còn sống " + alive;
        }

        public void SetTarget(bool has, in SpectatorEntry entry)
        {
            var name = has ? entry.Name ?? "Người chơi " + (entry.NetId + 1) : "—";
            if (!ReferenceEquals(name, _shownName) && name != _shownName)
            {
                _shownName = name;
                watchingName.text = name;
                cardName.text = name;
            }
            var showHp = has && entry.HasHp;
            if (cardHp.activeSelf != showHp) cardHp.SetActive(showHp);
            if (showHp && (entry.Hp != _shownHp || entry.MaxHp != _shownMaxHp))
            {
                _shownHp = entry.Hp;
                _shownMaxHp = entry.MaxHp;
                cardHpFill.anchorMax = new Vector2(entry.MaxHp > 0 ? (float)entry.Hp / entry.MaxHp : 0f, 1f);
                cardHpText.text = entry.Hp + "/" + entry.MaxHp;
            }
            var weapon = has ? entry.WeaponName : null;
            if (cardWeapon.gameObject.activeSelf != (weapon != null)) cardWeapon.gameObject.SetActive(weapon != null);
            if (weapon != null && weapon != _shownWeapon) { _shownWeapon = weapon; cardWeapon.text = weapon; }
        }

        public void ResetCache()
        {
            _shownAlive = _shownStart = _shownHp = _shownMaxHp = -1;
            _shownName = _shownWeapon = null;
        }
    }
}
