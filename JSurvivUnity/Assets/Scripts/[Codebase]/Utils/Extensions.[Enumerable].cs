using System;
using System.Collections.Generic;

namespace _Codebase_.Utils
{
    public static class EnumerableExtensions
    {
        public static void ForEach<T>(this IEnumerable<T> array, Action<T> action)
        {
            foreach (var item in array) action(item);
        }
    }
}