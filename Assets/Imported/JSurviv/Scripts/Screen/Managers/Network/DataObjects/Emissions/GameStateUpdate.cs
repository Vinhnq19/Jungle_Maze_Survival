namespace Managers.DataObjects
{
    public class GameStateUpdate
    {

        public class Request : IEmission<Response>
        {
            public int safeZoneRadius { get; set; }
            public EntitySchema[] entities { get; set; }
            public PlayerSchema[] players { get; set; }
            public bool isAccentWallOpened { get; set; }
            public string EventName => "game:state:update";
        }

        public class Response : BaseResponse
        {
        
        }
    }
}