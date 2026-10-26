using NUnit.Framework;
using JungleMaze.Domain.Hud;

namespace JungleMaze.Tests.Hud.Feedback
{
    public sealed class FeedbackDomainTests
    {
        [Test] public void EffectKinds_PreserveOldValues_AndAdrenalineHasNoChip()
        {
            Assert.AreEqual(1, (int)EffectKind.Adrenaline); Assert.False(EffectKinds.ShowsAsChip(EffectKind.Adrenaline));
            Assert.AreEqual(EffectPolarity.Harmful, EffectKinds.PolarityOf(EffectKind.Poison));
            Assert.AreEqual(EffectPolarity.Beneficial, EffectKinds.PolarityOf(EffectKind.CcImmune));
        }
        [Test] public void DangerList_RefreshesInPlace_AndExpiresExactlyAtZero()
        {
            var list = new DangerList(); var expired = -1;
            list.Apply(new DangerWarning(1, DangerKind.FusedBomb, 1.5f, 1.5f, true, 7, 7), out _);
            list.Apply(new DangerWarning(2, DangerKind.Trap, 0, 0, false, 0, 0), out _);
            list.Tick(1, id => expired = id); Assert.AreEqual(.5f, list.GetAt(0).RemainingSeconds);
            list.Apply(new DangerWarning(1, DangerKind.FusedBomb, 1.5f, 1, true, 8, 8), out var type);
            Assert.AreEqual(DangerChangeType.Refreshed, type); Assert.AreEqual(1, list.GetAt(0).RemainingSeconds);
            list.Tick(1, id => expired = id); Assert.AreEqual(1, expired); Assert.AreEqual(1, list.Count); Assert.AreEqual(2, list.GetAt(0).Id);
        }
        [Test] public void DangerList_CapacityRemovalAndInvalidValues()
        {
            var list = new DangerList();
            for (var i = 0; i < 16; i++) Assert.True(list.Apply(new DangerWarning(i, DangerKind.Trap, 0, 0, false, 0, 0), out _));
            Assert.False(list.Apply(new DangerWarning(17, DangerKind.Trap, 0, 0, false, 0, 0), out _));
            Assert.True(list.Remove(3)); Assert.False(list.Remove(3)); Assert.AreEqual(4, list.GetAt(3).Id);
            Assert.False(list.Apply(new DangerWarning(20, DangerKind.FusedBomb, float.NaN, 1, false, 0, 0), out _));
            list.Clear(); Assert.AreEqual(0, list.Count);
        }
        [Test] public void ItemUse_TicksToZeroWithoutCompleting_ResyncsAndIgnoresStaleFinish()
        {
            var use = new ItemUseTracker(); use.Begin(new ItemUseStatus(ItemUsePhase.Using,"A","A",4,.5f,.5f,true));
            use.Tick(.25f); Assert.AreEqual(.25f,use.Status.RemainingSeconds);
            use.Tick(1f); Assert.AreEqual(0,use.Status.RemainingSeconds); Assert.AreEqual(ItemUsePhase.Using,use.Status.Phase);
            use.Begin(new ItemUseStatus(ItemUsePhase.Using,"B","B",5,3.5f,2,true));
            Assert.False(use.Cancel("A")); Assert.False(use.Complete("A"));
            use.Begin(new ItemUseStatus(ItemUsePhase.Using,"B","B",5,3.5f,3,true)); Assert.AreEqual(3,use.Status.RemainingSeconds);
            Assert.True(use.Complete("B")); Assert.AreEqual(ItemUsePhase.Completed,use.Status.Phase); Assert.True(use.Reset());
        }
        [Test] public void ItemUse_RejectsInvalidStart()
        {
            var use = new ItemUseTracker(); Assert.False(use.Begin(default));
            Assert.False(use.Begin(new ItemUseStatus(ItemUsePhase.Using,"A","A",8,1,1,true)));
            Assert.False(use.Begin(new ItemUseStatus(ItemUsePhase.Using,"A","A",1,float.PositiveInfinity,1,true)));
        }
    }
}
