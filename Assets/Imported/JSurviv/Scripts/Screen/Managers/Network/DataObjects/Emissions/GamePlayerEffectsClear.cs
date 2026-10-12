namespace Managers.DataObjects
{
    public class GamePlayerEffectsClear
    {

        public class Request : IEmission<Response>
        {
            public string playerId { get; set; }
            public string effect { get; set; }
            public string EventName => "game:player:effects:clear";
        }

        public class Response : BaseResponse
        {
        }
    }
}