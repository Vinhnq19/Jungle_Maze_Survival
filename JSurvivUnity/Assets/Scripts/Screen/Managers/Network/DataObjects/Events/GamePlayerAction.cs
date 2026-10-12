using GameDevWare.Serialization;

namespace Managers.DataObjects
{
    public class GamePlayerAction
    {
        public const string EventName = "game:player:action";

        public class Response
        {
            public string sessionId { get; set; }
            public string playerId { get; set; }
            public string action { get; set; }
            public IndexedDictionary<string,object> attributes { get; set; }
        }
    }
}