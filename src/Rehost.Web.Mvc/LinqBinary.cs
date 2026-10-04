using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace System.Web.Mvc
{
    internal static class LinqBinary
    {
        public static readonly LinqBinaryModelBinder ModelBinder = new LinqBinaryModelBinder();
        private static readonly ConcurrentDictionary<Type, Func<object, byte[]>> _toArray = new ConcurrentDictionary<Type, Func<object, byte[]>>();
        private static readonly ConcurrentDictionary<Type, Func<byte[], object>> _create = new ConcurrentDictionary<Type, Func<byte[], object>>();

        public static bool IsBinaryType(Type type)
        {
            return type != null && type.FullName == "System.Data.Linq.Binary";
        }

        public static bool TryToArray(object value, out byte[] bytes)
        {
            if (value == null || !IsBinaryType(value.GetType()))
            {
                bytes = null;
                return false;
            }

            bytes = _toArray.GetOrAdd(value.GetType(), CompileToArray)(value);
            return true;
        }

        public static object Create(Type type, byte[] bytes)
        {
            return _create.GetOrAdd(type, CompileCreate)(bytes);
        }

        private static Func<object, byte[]> CompileToArray(Type type)
        {
            var value = Expression.Parameter(typeof(object), "value");
            var call = Expression.Call(Expression.Convert(value, type), type.GetMethod("ToArray", Type.EmptyTypes));
            return Expression.Lambda<Func<object, byte[]>>(call, value).Compile();
        }

        private static Func<byte[], object> CompileCreate(Type type)
        {
            var bytes = Expression.Parameter(typeof(byte[]), "bytes");
            var construct = Expression.New(type.GetConstructor(new[] { typeof(byte[]) }), bytes);
            return Expression.Lambda<Func<byte[], object>>(Expression.Convert(construct, typeof(object)), bytes).Compile();
        }
    }
}
