namespace Managers.DataObjects
{
    public class GamePlayerEffectsApply
    {
        public class Request : IEmission<Response>
        {

            public string playerId { get; set; }
            public EffectSchema effect { get; set; }
            public string EventName => "game:player:effects:apply";
        }

        public class Response : BaseResponse
        {
        }
    }
}