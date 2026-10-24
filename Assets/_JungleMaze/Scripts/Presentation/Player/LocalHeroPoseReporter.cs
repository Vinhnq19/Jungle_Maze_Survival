using JungleMaze.App.Hud.State;
using JungleMaze.Core.DI;
using UnityEngine;

namespace JungleMaze.Presentation.Player
{
    /// <summary>
    /// Writes the local Hero's position and facing into the HUD state every frame (minimap marker, AC15).
    /// Runs before other Updates so the HUD (LateUpdate) reads this frame's pose. Reads no input and never moves the
    /// Hero. Without injection (tests, scenes without Bootstrap) it does nothing.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class LocalHeroPoseReporter : MonoBehaviour
    {
        private IHudStateWriter _hud;

        [Inject]
        public void Construct(IHudStateWriter hud) => _hud = hud;

        private void Update() => Report();

        /// <summary>Pushes the current transform; also callable by tests and by MOV-02's predicted Hero.</summary>
        public void Report()
        {
            if (_hud == null) return;
            var position = transform.position;
            _hud.SetPose(position.x, position.y, transform.eulerAngles.z);
        }
    }
}
