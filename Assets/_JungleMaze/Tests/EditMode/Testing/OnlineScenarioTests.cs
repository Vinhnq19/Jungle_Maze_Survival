using JungleMaze.App.Testing;
using JungleMaze.Infrastructure.Testing;
using NUnit.Framework;

namespace JungleMaze.Tests.Testing
{
    public sealed class OnlineScenarioTests
    {
        [Test] public void MainCanWaitForFullRoundWithoutAcceleratingServer()
        { var s = new OnlineScenario { id = "main6", expectedPlayers = 6, maxDurationSeconds = 600, minConnectedSeconds = 300, requireResult = true }; Assert.IsNull(s.Validate()); }
        [TestCase(0)] [TestCase(11)] public void RefusesImpossiblePlayerCount(int players)
        { Assert.IsNotNull(new OnlineScenario { expectedPlayers = players }.Validate()); }
        [Test] public void DeadlineMustAllowConnectionWindow()
        { Assert.IsNotNull(new OnlineScenario { maxDurationSeconds = 30, minConnectedSeconds = 30 }.Validate()); }
        [Test] public void CannotClaimReplayWithoutRequiringResult()
        { Assert.IsNotNull(new OnlineScenario { requireReplay = true, requireResult = false }.Validate()); }
        [Test] public void CannotSilentlyDuplicateFutureFeatureCheck()
        { Assert.IsNotNull(new OnlineScenario { extraAssertions = new[] { "combat.damage", "combat.damage" } }.Validate()); }
        [Test] public void MissingFeatureIsNotAutomaticallyPassed()
        { Assert.IsFalse(OnlineTestExtensions.TryGetCheck("not-implemented.feature", out _)); }
        [Test] public void ExtensionHasExplicitPendingAndFailureStates()
        { Assert.AreEqual("WAIT", new OnlineCheckResult("WAIT").Status); Assert.AreEqual("FAIL", new OnlineCheckResult("FAIL", "damage not authoritative").Status); Assert.Throws<System.ArgumentException>(() => new OnlineCheckResult("SKIP_AS_PASS")); }
        [Test] public void UnknownFutureCombatObjectMustNotBeSilentlyIgnored()
        { Assert.IsFalse(OnlineScenarioJson.TryParse("{\"schemaVersion\":1,\"id\":\"combat\",\"extensions\":{\"combat\":{\"requireKills\":1}}}", out _, out _)); }
        [Test] public void MissingOptionalFieldsRetainExplicitDefaults()
        { Assert.IsTrue(OnlineScenarioJson.TryParse("{\"schemaVersion\":1,\"id\":\"defaults\"}", out var s, out _)); Assert.AreEqual("movement", s.driver); Assert.AreEqual(28.5f, s.minInputHz); }
    }
}
