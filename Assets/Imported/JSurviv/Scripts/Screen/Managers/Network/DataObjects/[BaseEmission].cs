namespace Managers.DataObjects
{
    public interface IEmission<T>
    {
        public string EventName { get; }
    }

    public class BaseResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }
}