using System;
using System.IO;
using System.Linq;
using _Codebase_.Command;
using _Codebase_.Configs;
using _Codebase_.Entity.Player;
using _Codebase_.Utils;
using Cysharp.Threading.Tasks;
using GameDevWare.Serialization;
using Managers.DataObjects;
using Newtonsoft.Json.Serialization;
using SocketIOClient;
using SocketIOClient.Newtonsoft.Json;
using SocketIOClient.Transport;
using UnityEngine;

namespace Managers
{
    public class NetworkManager : MonoBehaviour
    {
        private SocketIOUnity socket;
        public bool IsConnected => this.socket?.Connected ?? false;
        private Action onConnectedCallback;

        private void OnEnable()
        {
            this.InitSocket();
        }

        private void OnDisable()
        {
            this.DisposeSocket();
        }

        private void InitSocket()
        {
            if (this.socket != null) return;

            this.socket = new(GameConfig.I.serverUrl, new SocketIOOptions
            {
                Auth = new
                {
                    GameConfig.I.secretKey,
                },
                Transport = TransportProtocol.WebSocket,
            })
            {
                JsonSerializer = new NewtonsoftJsonSerializer
                {
                    OptionsProvider = () => new()
                    {
                        ContractResolver = new CamelCasePropertyNamesContractResolver(),
                    },
                },
            };

            this.socket.OnConnected += this.OnConnected;
            this.socket.OnDisconnected += this.OnDisconnected;
            this.socket.OnReconnected += this.OnReconnected;

            this.On<GamePlayerJoined.Response>(GamePlayerJoined.EventName, this.OnGamePlayerJoined);
            this.On<GamePlayerLeft.Response>(GamePlayerLeft.EventName, this.OnGamePlayerLeft);
            this.On<GamePlayerAction.Response>(GamePlayerAction.EventName, this.OnGamePlayerAction);


            Debug.Log($"NetworkManager.InitSocket(): Connecting to {GameConfig.I.serverUrl}");
            this.socket.Connect();
        }

        private void DisposeSocket()
        {
            if (this.socket == null) return;

            this.Off(GamePlayerJoined.EventName);
            this.Off(GamePlayerLeft.EventName);
            this.Off(GamePlayerAction.EventName);

            Debug.Log("NetworkManager.DisposeSocket(): Disconnecting");
            this.socket.Disconnect();
            this.socket.Dispose();
            this.socket = null;
        }


        public void ResetConnection()
        {
            Debug.Log("Resetting network connection...");
            DisposeSocket();
            UniTask.Delay(TimeSpan.FromMilliseconds(100)).ContinueWith(InitSocket).Forget();
        }

        private void OnReconnected(object sender, int e)
        {
            Debug.Log($"NetworkManager.OnReconnected({sender}, {e})");
        }


        private void OnDisconnected(object sender, string e)
        {
            Debug.Log($"NetworkManager.OnDisconnected({sender}, {e})");
        }

        public void ExecuteOnConnected(Action callback)
        {
            if (IsConnected)
            {
                callback?.Invoke();
            }
            else
            {
                onConnectedCallback += callback;
            }
        }

        private void OnConnected(object sender, EventArgs e)
        {
            Debug.Log($"NetworkManager.OnConnected event fired!");

            // Đảm bảo rằng chúng ta gửi yêu cầu tạo session trên main thread của Unity
            GameManager.I.RunOnMainThread(() =>
            {
                if (GameManager.I.CurrentGameId.HasValue)
                {
                    Debug.Log($"Sending GameSessionCreate request with new join code: {GameManager.I.CurrentGameId.Value}");
                    this.Send(new GameSessionCreate.Request
                    {
                        joinCode = GameManager.I.CurrentGameId.ToString(),
                    });
                }
                else
                {
                    Debug.LogWarning("Cannot create game session, CurrentGameId is null.");
                }
            });
        }

        public void On<TResponse>(string eventName, Action<TResponse> onResponse)
        {
            this.socket.OnUnityThread(eventName, response =>
            {
                var bytes = response.GetValue<byte[]>();
                using var stream = new MemoryStream(bytes);

                var res = MsgPack.Deserialize<TResponse>(stream);

                onResponse(res);
            });
        }

        public void Off(string eventName)
        {
            this.socket?.Off(eventName);
        }

        public void Send<TResponse>(IEmission<TResponse> request, Action<TResponse> onResponse = null)
        {
            if (this.socket == null || !this.socket.Connected)
            {
                Debug.LogError("Lost Connection or socket is null. Cannot send message.");
                return;
            }

            var packetId = this.socket.ForceGetField<int>("_packetId") + 1;
            Debug.Log($"NetworkManager.Send({packetId}, \"{request.EventName}\")");

            this.socket.Emit(request.EventName, response =>
            {
                Debug.Log($"NetworkManager.Receive({packetId}, \"{request.EventName}\")\n{response}");
                if (onResponse is null) return;
                onResponse(response.GetValue<TResponse>());
            }, request);
        }


        #region Events

        private void OnGamePlayerAction(GamePlayerAction.Response obj)
        {
            Debug.Log($"NetworkManager.OnGamePlayerAction({obj.playerId}, {obj.action})");

            var player = PlayerManager.I.Players.FirstOrDefault(p => p.Id == obj.playerId);

            if (player)
            {
                ProcessPlayerAction(obj, player);
            }
        }

        private void ProcessPlayerAction(GamePlayerAction.Response obj, PlayerEntity playerEntity)
        {
            switch (obj.action)
            {
                case "move":
                    playerEntity.MovementQueue.Clear();
                    if (obj.attributes["directions"] is not string { Length: > 0 } directions) return;
                    foreach (var direction in directions)
                    {
                        playerEntity.MovementQueue.Enqueue(new MoveCommand(playerEntity, direction));
                    }

                    break;
                case "attack":
                    if (obj.attributes["direction"] is not string attackDirection ||
                        attackDirection.Length == 0) return;
                    playerEntity.PlayerAction = new MeleeCommand(playerEntity, attackDirection[0]);
                    break;
                case "shoot":
                    if (obj.attributes["direction"] is not string shootDirection || 
                        shootDirection.Length == 0) return;
                    playerEntity.PlayerAction = new ShootCommand(playerEntity, shootDirection[0]);
                    break;
                case "throw":
                    if (obj.attributes["direction"] is not string throwDirection ||
                        throwDirection.Length == 0) return;
                    playerEntity.PlayerAction = new ThrowCommand(playerEntity, throwDirection[0]);
                    break;
                case "special":
                    if (obj.attributes["direction"] is not string specialDirection ||
                        specialDirection.Length == 0) return;
                    playerEntity.PlayerAction = new SpecialCommand(playerEntity, specialDirection[0]);
                    break;
                case "pick":
                    playerEntity.PlayerAction = new PickUpCommand(playerEntity);
                    break;
                case "use":
                    if (obj.attributes["itemId"] is not string useItemId) break;
                    playerEntity.PlayerAction = new UseItemCommand(playerEntity, useItemId);
                    break;
                case "drop":
                    if (obj.attributes["itemId"] is not string dropItemId) break;
                    playerEntity.PlayerAction = new RevokeCommand(playerEntity, dropItemId);
                    break;
            }
        }

        private void OnGamePlayerLeft(GamePlayerLeft.Response obj)
        {
            Debug.Log($"NetworkManager.OnGamePlayerLeft({obj.playerId})");
            LobbyManager.I.RemovePlayer(obj.playerId);
        }

        private void OnGamePlayerJoined(GamePlayerJoined.Response obj)
        {
            Debug.Log($"NetworkManager.OnGamePlayerJoined({obj.playerId})");
            LobbyManager.I.AddPlayer(obj.playerId, obj.playerName);
        }

        #endregion

        #region Singleton

        public static NetworkManager I { get; private set; }

        private void Awake()
        {
            I = this;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void RuntimeInitializeOnLoadMethod()
        {
            I = null;
        }

        #endregion
    }
}
