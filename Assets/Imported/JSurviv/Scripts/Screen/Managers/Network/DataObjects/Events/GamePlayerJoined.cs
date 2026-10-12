namespace Managers.DataObjects
{
    public class GamePlayerJoined
    {
        public const string EventName = "game:player:joined";
        
        public class Response
        {
            public string playerId { get; set; }
            public string playerName { get; set; }
        }
    }
}