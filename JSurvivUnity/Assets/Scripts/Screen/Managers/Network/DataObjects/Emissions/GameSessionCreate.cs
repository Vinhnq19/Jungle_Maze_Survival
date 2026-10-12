namespace Managers.DataObjects
{
    public class GameSessionCreate
    {

        public class Request : IEmission<Response>
        {
            public string joinCode { get; set; }
            public string EventName => "game:session:create";
        }

        public class Response : BaseResponse
        {
            public string sessionId { get; set; }
        }
    }
}