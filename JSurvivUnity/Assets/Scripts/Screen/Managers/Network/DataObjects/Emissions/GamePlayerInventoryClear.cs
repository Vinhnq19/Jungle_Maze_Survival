namespace Managers.DataObjects
{
    public class GamePlayerInventoryClear
    {

        public class Request : IEmission<Response>
        {
            public string playerId { get; set; }
            
            public string id { get; set; }
            public string type { get; set; }
            public string EventName => "game:player:inventory:clear";
        }

        public class Response : BaseResponse
        {
        }
    }
}