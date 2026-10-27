using System;
using JungleMaze.App.MatchUi;
using JungleMaze.Domain.MatchUi;
using JungleMaze.Presentation.UI.Hud;
using UnityEngine;

namespace JungleMaze.Presentation.UI.MatchOverlay
{
    /// <summary>
    /// G-11: while spectating, the local player's Vitals and Quick bar (UI-01) are switched off; Minimap and match
    /// status stay. Touches only the two root GameObjects through <see cref="HudRoot"/>'s public getters, and puts back
    /// exactly what it switched off. No change in UI-01 code.
    /// </summary>
    public sealed class HudSpectatorMask : IDisposable
    {
        private readonly MatchOverlayFlow _flow;
        private readonly Func<HudRoot> _findHud;
        private readonly Action<MatchOverlayPhase> _onPhase;
        private GameObject _vitals, _quickbar;

        public HudSpectatorMask(MatchOverlayFlow flow, Func<HudRoot> findHud = null)
        {
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _findHud = findHud ?? UnityEngine.Object.FindAnyObjectByType<HudRoot>;
            _onPhase = OnPhase;
            _flow.PhaseChanged += _onPhase;
        }

        public bool Masking => _vitals != null || _quickbar != null;

        public void Dispose()
        {
            _flow.PhaseChanged -= _onPhase;
            Restore();
        }

        private void OnPhase(MatchOverlayPhase phase)
        {
            if (phase == MatchOverlayPhase.Spectating) Mask();
            else Restore();
        }

        private void Mask()
        {
            var hud = _findHud();
            if (hud == null) return;
            _vitals = Hide(hud.Vitals != null ? hud.Vitals.gameObject : null);
            _quickbar = Hide(hud.Quickbar != null ? hud.Quickbar.gameObject : null);
        }

        private void Restore()
        {
            if (_vitals != null) _vitals.SetActive(true);
            if (_quickbar != null) _quickbar.SetActive(true);
            _vitals = _quickbar = null;
        }

        private static GameObject Hide(GameObject go)
        {
            if (go == null || !go.activeSelf) return null;
            go.SetActive(false);
            return go;
        }
    }
}
