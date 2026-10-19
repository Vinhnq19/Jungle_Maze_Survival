using System;

namespace JungleMaze.Core.States
{
    /// <summary>State Pattern: dùng chung cho vòng đời app (menu, kết nối, trận) và AI/nhân vật.</summary>
    public interface IState
    {
        void Enter();
        void Tick(float deltaTime);
        void Exit();
    }

    public sealed class StateMachine
    {
        private IState _pending;
        private bool _transitioning;

        public IState Current { get; private set; }

        /// <summary>(previous, next) sau khi next.Enter() đã chạy xong.</summary>
        public event Action<IState, IState> StateChanged;

        public void ChangeState(IState next)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            // Đổi state ngay trong Enter/Exit sẽ được xếp hàng, tránh Enter lồng nhau khó lần lỗi.
            if (_transitioning) { _pending = next; return; }
            _transitioning = true;
            try
            {
                var target = next;
                while (target != null)
                {
                    _pending = null;
                    var previous = Current;
                    previous?.Exit();
                    Current = target;
                    target.Enter();
                    StateChanged?.Invoke(previous, target);
                    target = _pending;
                }
            }
            finally
            {
                _transitioning = false;
                _pending = null;
            }
        }

        public void Tick(float deltaTime) => Current?.Tick(deltaTime);

        /// <summary>Thoát state hiện tại khi tắt game; không chuyển sang state nào.</summary>
        public void Stop()
        {
            var previous = Current;
            if (previous == null) return;
            Current = null;
            previous.Exit();
        }
    }
}
