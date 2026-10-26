using System;

namespace JungleMaze.Domain.Hud
{
    /// <summary>Tracks source actions without completing them on the HUD's behalf.</summary>
    public sealed class ItemUseTracker
    {
        public ItemUseStatus Status { get; private set; }
        public bool Begin(in ItemUseStatus status)
        {
            if (string.IsNullOrEmpty(status.ItemId) || status.SlotIndex < 0 || status.SlotIndex >= 8 || !(status.TotalSeconds > 0f) || float.IsInfinity(status.TotalSeconds) || float.IsNaN(status.RemainingSeconds) || float.IsInfinity(status.RemainingSeconds) || status.RemainingSeconds < 0f) return false;
            Status = new ItemUseStatus(ItemUsePhase.Using, status.ItemId, status.DisplayName, status.SlotIndex, status.TotalSeconds, Math.Min(status.TotalSeconds, status.RemainingSeconds), status.Cancellable);
            return true;
        }
        public bool Cancel(string id) => Finish(id, ItemUsePhase.Cancelled);
        public bool Complete(string id) => Finish(id, ItemUsePhase.Completed);
        public bool Reset() { if (Status.Phase == ItemUsePhase.Idle) return false; Status = default; return true; }
        public void Tick(float dt)
        {
            if (Status.Phase != ItemUsePhase.Using || !(dt > 0f) || float.IsInfinity(dt)) return;
            var s = Status;
            Status = new ItemUseStatus(s.Phase, s.ItemId, s.DisplayName, s.SlotIndex, s.TotalSeconds, Math.Max(0f, s.RemainingSeconds - dt), s.Cancellable);
        }
        private bool Finish(string id, ItemUsePhase phase)
        {
            var s = Status;
            if (s.Phase != ItemUsePhase.Using || s.ItemId != id) return false;
            Status = new ItemUseStatus(phase, s.ItemId, s.DisplayName, s.SlotIndex, s.TotalSeconds, phase == ItemUsePhase.Completed ? 0f : s.RemainingSeconds, s.Cancellable);
            return true;
        }
    }
}
