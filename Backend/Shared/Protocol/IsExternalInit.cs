// Unity (netstandard2.1) thiếu kiểu này nên record/init không compile; net5+ đã có sẵn.
#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
