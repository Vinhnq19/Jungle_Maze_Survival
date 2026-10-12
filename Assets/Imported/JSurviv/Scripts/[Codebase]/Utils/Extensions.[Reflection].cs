using System.Reflection;

namespace _Codebase_.Utils
{
    public static class ReflectionExtensions
    {
        public static void ForceSetField(this object obj, string fieldName, object value)
        {
            var type = obj.GetType();

            var field = type.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
            );

            field?.SetValue(obj, value);
        }

        public static T ForceGetField<T>(this object obj, string fieldName)
        {
            var type = obj.GetType();

            while (type is not null)
            {
                var field = type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                );

                if (field is not null)
                {
                    return (T)(field.GetValue(obj) ?? default(T));
                }

                type = type.BaseType;
            }

            return default;
        }
    }
}