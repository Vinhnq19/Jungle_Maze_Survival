namespace Managers.DataObjects
{
    public class GamePlayerInventoryAdd
    {

        public class Request : IEmission<Response>
        {
            public string playerId { get; set; }
            public ItemSchema item { get; set; }
            public string EventName => "game:player:inventory:add";
        }

        public class Response : BaseResponse
        {
        }
    }
}