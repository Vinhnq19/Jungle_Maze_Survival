namespace Managers.DataObjects
{
    public class GamePlayerLeft
    {
        public const string EventName = "game:player:left";

        public class Response
        {
            public string playerId { get; set; }
        }
    }
}