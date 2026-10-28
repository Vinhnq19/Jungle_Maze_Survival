using System;
using JungleBackend.Protocol.Gameplay;
using JungleMaze.App.Player;
using JungleMaze.App.Ports;
using JungleMaze.App.States;
using JungleMaze.Core.DI;
using JungleMaze.Core.Events;
using JungleMaze.Core.States;
using JungleMaze.Core.Ticking;
using JungleMaze.Domain.Network;

namespace JungleMaze.App.Flow
{
    /// <summary>
    /// Vòng đời client theo State Pattern. Event mạng/gameplay được chuyển cho state hiện tại xử lý,
    /// nên mỗi state tự quyết phản ứng (ví dụ MatchStarted chỉ có nghĩa khi đang InRoom).
    /// </summary>
    public sealed class GameFlow : IInitializable, ITickable, IDisposable
    {
        private readonly IEventBus _bus;
        private readonly IPacketReceiver _packets;
        private readonly StateMachine _machine = new StateMachine();
        private readonly Action<NetworkStateChanged> _onNetwork;
        private readonly Action<EventPacket> _onGameEvent;
        public bool ServerDriven { get; private set; }

        public GameFlow(IEventBus bus, INetworkClient network, IPacketReceiver packets, ISceneLoader scenes,
            PlayerInputService input, GameFlowSettings settings = null)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _packets = packets ?? throw new ArgumentNullException(nameof(packets));
            if (network == null) throw new ArgumentNullException(nameof(network));
            if (scenes == null) throw new ArgumentNullException(nameof(scenes));
            Settings = settings ?? new GameFlowSettings();

            Boot = new BootState(this);
            MainMenu = new MainMenuState(this, scenes);
            Connecting = new ConnectingState(this, network);
            InRoom = new InRoomState(this, scenes);
            InMatch = new InMatchState(this, input);
            Results = new ResultsState(this);

            _onNetwork = e => (_machine.Current as GameState)?.OnNetworkStateChanged(e);
            _onGameEvent = e => (_machine.Current as GameState)?.OnGameEvent(e);

        }

        public GameFlowSettings Settings { get; }
        public AppScreen Screen { get; private set; } = AppScreen.Boot;
        public string Message { get; private set; } = "";
        public IState CurrentState => _machine.Current;

        internal BootState Boot { get; }
        internal MainMenuState MainMenu { get; }
        internal ConnectingState Connecting { get; }
        internal InRoomState InRoom { get; }
        internal InMatchState InMatch { get; }
        internal ResultsState Results { get; }

        /// <summary>Called only after MatchSession rejects stale match packets.</summary>
        public void ApplyAuthoritativePhase(MatchPhasePacket p)
        {
            ServerDriven = true;
            if (p.Phase == MatchPhase.InGame && _machine.Current != InMatch) Go(InMatch);
            else if (p.Phase == MatchPhase.Ending && _machine.Current != Results) Go(Results);
            else if ((p.Phase == MatchPhase.Waiting || p.Phase == MatchPhase.Lobby || p.Phase == MatchPhase.Loading) && _machine.Current != InRoom) Go(InRoom);
        }

        public void Initialize()
        {
            _bus.Subscribe(_onNetwork);
            _packets.Subscribe(_onGameEvent);
            _machine.ChangeState(Settings.StartLocalPreview ? InMatch : (IState)Boot);
        }

        public void Tick(float deltaTime) => _machine.Tick(deltaTime);

        public void Dispose()
        {
            _bus.Unsubscribe(_onNetwork);
            _packets.Unsubscribe(_onGameEvent);
            _machine.Stop();
        }

        internal void Go(GameState next) => _machine.ChangeState(next);

        /// <summary>State gọi khi Enter để Presentation đổi view.</summary>
        internal void Show(AppScreen screen, string message = "")
        {
            var previous = Screen;
            Screen = screen;
            Message = message ?? "";
            _bus.Publish(new AppScreenChanged(previous, screen, Message));
        }
    }
}
