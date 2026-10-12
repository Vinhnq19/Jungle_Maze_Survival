namespace Managers.DataObjects
{
    public class GameStatusFinish
    {

        public class Request : IEmission<Response>
        {
            public string EventName => "game:status:finish";
        }

        public class Response : BaseResponse
        {
            public string endTime { get; set; }
        }
    }
}