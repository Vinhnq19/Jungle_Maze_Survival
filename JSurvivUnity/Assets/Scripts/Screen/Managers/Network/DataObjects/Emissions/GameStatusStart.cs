namespace Managers.DataObjects
{
    public class GameStatusStart
    {

        public class Request : IEmission<Response>
        {
            public string EventName => "game:status:start";
        }

        public class Response : BaseResponse
        {
            public string startTime { get; set; }
        }
    }
}