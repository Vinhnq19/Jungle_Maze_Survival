namespace Managers.DataObjects
{
    public class GamePlayerKick
    {

        public class Request : IEmission<Response>
        {
            public string playerId { get; set; }
            public string EventName => "game:player:kick";
        }

        public class Response : BaseResponse
        {
        }
    }
}