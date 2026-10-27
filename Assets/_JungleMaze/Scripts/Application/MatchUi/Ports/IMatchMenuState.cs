using System;

namespace JungleMaze.App.MatchUi
{
    /// <summary>
    /// N5: whether the in-match ESC menu is open. MOV-02 reads <see cref="BlocksGameplayInput"/> and sends neutral input
    /// while it is true (no move, no attack, Q6); until then UI-03's own guard does it through public API (T-1 = A).
    /// </summary>
    public interface IMatchMenuState
    {
        bool IsOpen { get; }

        /// <summary>True while the menu (or its confirm box) is open inside a match.</summary>
        bool BlocksGameplayInput { get; }

        event Action<bool> OpenChanged;
    }
}
