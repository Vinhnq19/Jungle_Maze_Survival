using System.Collections.Generic;
using JungleMaze.Core.States;
using NUnit.Framework;

namespace JungleMaze.Tests.Core
{
    public sealed class StateMachineTests
    {
        private sealed class RecordingState : IState
        {
            private readonly string _name;
            private readonly List<string> _log;
            public System.Action OnEnter;
            public RecordingState(string name, List<string> log) { _name = name; _log = log; }
            public void Enter() { _log.Add("enter:" + _name); OnEnter?.Invoke(); }
            public void Tick(float deltaTime) => _log.Add("tick:" + _name);
            public void Exit() => _log.Add("exit:" + _name);
        }

        [Test]
        public void ChangeState_ExitsPreviousThenEntersNext()
        {
            var log = new List<string>();
            var machine = new StateMachine();
            machine.ChangeState(new RecordingState("a", log));
            machine.Tick(0.1f);
            machine.ChangeState(new RecordingState("b", log));
            CollectionAssert.AreEqual(new[] { "enter:a", "tick:a", "exit:a", "enter:b" }, log);
        }

        [Test]
        public void ChangeStateInsideEnter_IsQueued_NotNested()
        {
            var log = new List<string>();
            var machine = new StateMachine();
            var b = new RecordingState("b", log);
            var a = new RecordingState("a", log) { OnEnter = () => machine.ChangeState(b) };
            machine.ChangeState(a);
            Assert.AreSame(b, machine.Current);
            CollectionAssert.AreEqual(new[] { "enter:a", "exit:a", "enter:b" }, log);
        }

        [Test]
        public void StateChanged_ReportsPreviousAndNext()
        {
            var machine = new StateMachine();
            var log = new List<string>();
            var a = new RecordingState("a", log);
            var b = new RecordingState("b", log);
            IState seenPrevious = null, seenNext = null;
            machine.ChangeState(a);
            machine.StateChanged += (p, n) => { seenPrevious = p; seenNext = n; };
            machine.ChangeState(b);
            Assert.AreSame(a, seenPrevious);
            Assert.AreSame(b, seenNext);
        }

        [Test]
        public void Stop_ExitsCurrent()
        {
            var log = new List<string>();
            var machine = new StateMachine();
            machine.ChangeState(new RecordingState("a", log));
            machine.Stop();
            Assert.IsNull(machine.Current);
            CollectionAssert.AreEqual(new[] { "enter:a", "exit:a" }, log);
        }
    }
}
