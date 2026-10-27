using System;
using JungleMaze.App.MatchUi;
using JungleMaze.Domain.MatchUi;
using UnityEngine;

namespace JungleMaze.Presentation.UI.MatchOverlay
{
    /// <summary>
    /// UI-03 keys (Q11): ESC for the menu in every match phase; while spectating ←/A and →/D change player and E jumps
    /// to the killer; during the killcam Space skips. These keys are read only in those phases, when the local player is
    /// out and no gameplay input is sent (T-8). Nothing but ESC works while the menu is open. The key source is
    /// injectable so tests press keys without the Input Manager.
    /// </summary>
    public sealed class MatchOverlayInput
    {
        private readonly MatchMenuController _menu;
        private readonly MatchOverlayFlow _flow;
        private readonly Func<KeyCode, bool> _keyDown;

        public MatchOverlayInput(MatchMenuController menu, MatchOverlayFlow flow = null, Func<KeyCode, bool> keyDown = null)
        {
            _menu = menu ?? throw new ArgumentNullException(nameof(menu));
            _flow = flow;
            _keyDown = keyDown ?? UnityEngine.Input.GetKeyDown;
        }

        public void Poll()
        {
            if (_keyDown(KeyCode.Escape)) { _menu.PressEscape(); return; }
            if (_flow == null || _menu.IsOpen) return;
            switch (_flow.Phase)
            {
                case MatchOverlayPhase.Killcam:
                    if (_keyDown(KeyCode.Space)) _flow.SkipKillcam();
                    break;
                case MatchOverlayPhase.Spectating:
                    if (_keyDown(KeyCode.RightArrow) || _keyDown(KeyCode.D)) _flow.Targets.Next();
                    else if (_keyDown(KeyCode.LeftArrow) || _keyDown(KeyCode.A)) _flow.Targets.Previous();
                    else if (_keyDown(KeyCode.E)) _flow.Targets.JumpToKiller();
                    break;
            }
        }
    }
}
