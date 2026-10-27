using System;
using JungleMaze.App.MatchUi;
using UnityEngine;

namespace JungleMaze.Presentation.UI.MatchOverlay
{
    /// <summary>
    /// UI-03 keys (Q11): ESC for the menu in every match phase. The key source is injectable so tests press keys
    /// without the Input Manager.
    /// </summary>
    public sealed class MatchOverlayInput
    {
        private readonly MatchMenuController _menu;
        private readonly Func<KeyCode, bool> _keyDown;

        public MatchOverlayInput(MatchMenuController menu, Func<KeyCode, bool> keyDown = null)
        {
            _menu = menu ?? throw new ArgumentNullException(nameof(menu));
            _keyDown = keyDown ?? UnityEngine.Input.GetKeyDown;
        }

        public void Poll()
        {
            if (_keyDown(KeyCode.Escape)) _menu.PressEscape();
        }
    }
}
