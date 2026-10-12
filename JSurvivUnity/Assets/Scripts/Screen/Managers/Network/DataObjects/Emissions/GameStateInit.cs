namespace Managers.DataObjects
{
    public class GameStateInit
    {

        public class Request : IEmission<Response>
        {
            public int mapSize { get; set; }

            public ObstacleSchema[] obstacles { get; set; }
            public string EventName => "game:state:init";
        }

        public class Response : BaseResponse
        {
        }
    }
}