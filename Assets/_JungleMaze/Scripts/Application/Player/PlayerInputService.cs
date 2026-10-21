using System;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Ports;
using JungleMaze.Core.Ticking;
using JungleMaze.Domain.Player;

namespace JungleMaze.App.Player
{
    /// <summary>
    /// Gom intent từ Presentation và gửi PlayerInputPacket theo nhịp 30Hz của server (accumulator, không theo FPS).
    /// Chỉ gửi khi Enabled (InMatchState bật/tắt). Nút bấm được OR lại giữa hai lần gửi để không mất cú bấm ngắn.
    /// </summary>
    public sealed class PlayerInputService : ITickable
    {
        private const float SendInterval = 1f / GameplayProtocol.TickRate;
        private const int MaxCatchUp = 3;

        private readonly IPacketSender _sender;
        private PlayerIntent _intent;
        private InputButtons _latchedButtons;
        private float _accumulator;
        private bool _enabled;

        public PlayerInputService(IPacketSender sender) =>
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));

        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                _accumulator = 0f;
                _latchedButtons = InputButtons.None;
            }
        }

        public uint LocalTick { get; private set; }
        public ushort LastInputSeq { get; private set; }
        public int SentPackets { get; private set; }

        public void SetIntent(in PlayerIntent intent)
        {
            _intent = intent;
            _latchedButtons |= ToButtons(intent);
        }

        public void Tick(float deltaTime)
        {
            if (!_enabled) return;
            _accumulator += deltaTime;
            var steps = 0;
            while (_accumulator >= SendInterval && steps < MaxCatchUp)
            {
                _accumulator -= SendInterval;
                steps++;
                SendOne();
            }
            // Khung hình quá dài: bỏ phần dư thay vì gửi dồn hàng loạt input cũ.
            if (_accumulator >= SendInterval) _accumulator = 0f;
        }

        private void SendOne()
        {
            var packet = new PlayerInputPacket
            {
                Tick = ++LocalTick,
                InputSeq = ++LastInputSeq,
                Buttons = _latchedButtons | ToButtons(_intent)
            };
            packet.SetMove(_intent.MoveX, _intent.MoveY);
            packet.SetAimDegrees(_intent.AimDegrees);
            if (_sender.Send(ref packet, PacketDelivery.Unreliable)) SentPackets++;
            _latchedButtons = InputButtons.None;
        }

        private static InputButtons ToButtons(in PlayerIntent intent)
        {
            var buttons = InputButtons.None;
            if (intent.Attack) buttons |= InputButtons.Attack;
            if (intent.Interact) buttons |= InputButtons.Interact;
            if (intent.Reload) buttons |= InputButtons.Reload;
            if (intent.Dash) buttons |= InputButtons.Dash;
            if (intent.UseItem) buttons |= InputButtons.UseItem;
            return buttons;
        }
    }
}
