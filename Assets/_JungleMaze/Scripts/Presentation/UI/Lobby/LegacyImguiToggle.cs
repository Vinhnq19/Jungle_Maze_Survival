using UnityEngine;

namespace JungleMaze.Presentation.UI.Lobby
{
    /// <summary>
    /// The old IMGUI connection menu stays off while UI-02 runs (decision 8); F8 turns it back on for debugging.
    /// <c>MatchDemoView</c> keeps running (it sends LoadAck); hiding its drawing waits for Vinh's hook (T-1).
    /// </summary>
    public sealed class LegacyImguiToggle : MonoBehaviour
    {
        private System.Func<Behaviour> _find;
        private Behaviour _legacy;

        /// <summary>The menu is attached after composition, so it is looked up on the first frame.</summary>
        public void Construct(System.Func<Behaviour> findLegacyMenu) => _find = findLegacyMenu;

        private void Start()
        {
            _legacy = _find?.Invoke();
            if (_legacy != null) _legacy.enabled = false;
        }

        private void Update()
        {
            if (_legacy != null && Input.GetKeyDown(KeyCode.F8)) _legacy.enabled = !_legacy.enabled;
        }
    }
}
