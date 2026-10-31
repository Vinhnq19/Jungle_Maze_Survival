using System;
using JungleMaze.App.MatchUi;
using JungleMaze.Domain.MatchUi;
using JungleMaze.Presentation.UI.Hud;
using UnityEngine;

namespace JungleMaze.Presentation.UI.MatchOverlay
{
    /// <summary>
    /// UI-02 round 2 (UX review, replaces G-11): while the killcam or the spectator view is up, the whole UI-01 HUD
    /// canvas is switched off so nothing of the local player's HUD sits under those overlays. Touches only the canvas
    /// found from <see cref="HudRoot"/>, and turns back on exactly what it turned off. No change in UI-01 code.
    /// </summary>
    public sealed class HudSpectatorMask : IDisposable
    {
        private readonly MatchOverlayFlow _flow;
        private readonly Func<HudRoot> _findHud;
        private readonly Action<MatchOverlayPhase> _onPhase;
        private Canvas _canvas;

        public HudSpectatorMask(MatchOverlayFlow flow, Func<HudRoot> findHud = null)
        {
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _findHud = findHud ?? UnityEngine.Object.FindAnyObjectByType<HudRoot>;
            _onPhase = OnPhase;
            _flow.PhaseChanged += _onPhase;
        }

        public bool Masking => _canvas != null;

        public void Dispose()
        {
            _flow.PhaseChanged -= _onPhase;
            Restore();
        }

        private void OnPhase(MatchOverlayPhase phase)
        {
            if (phase == MatchOverlayPhase.Killcam || phase == MatchOverlayPhase.Spectating) Mask();
            else Restore();
        }

        private void Mask()
        {
            if (_canvas != null) return;
            var hud = _findHud();
            var canvas = hud != null ? hud.GetComponentInParent<Canvas>(true) : null;
            if (canvas == null || !canvas.enabled) return;
            canvas.enabled = false;
            _canvas = canvas;
        }

        private void Restore()
        {
            if (_canvas != null) _canvas.enabled = true;
            _canvas = null;
        }
    }
}
