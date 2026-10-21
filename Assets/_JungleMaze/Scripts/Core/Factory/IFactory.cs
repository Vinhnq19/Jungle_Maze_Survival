namespace JungleMaze.Core.Factory
{
    /// <summary>Factory tạo object đã đủ dependency; bản cho prefab lấy từ pool rồi inject.</summary>
    public interface IFactory<out TProduct>
    {
        TProduct Create();
    }

    // TArgs truyền bằng `in` (tránh copy struct lớn) nên phải invariant.
    public interface IFactory<TArgs, out TProduct>
    {
        TProduct Create(in TArgs args);
    }
}
