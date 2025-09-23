using System;
using System.Collections.Generic;
using System.Reflection;

namespace AutoCAC.Utilities
{
    public static class ObjectFactoryHelpers
    {
        public static T CreateStub<T>(string propertyName, object value)
        {
            var prop = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
                      ?? throw new ArgumentException($"Property '{propertyName}' not found on {typeof(T).FullName}");

            var stub = Activator.CreateInstance<T>()!;
            prop.SetValue(stub, value);
            return stub;
        }

        public static List<T> CreateStubs<T>(string propertyName, params object[] values)
        {
            var prop = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
                      ?? throw new ArgumentException($"Property '{propertyName}' not found on {typeof(T).FullName}");

            var list = new List<T>(values?.Length ?? 0);
            foreach (var value in values)
            {
                var stub = Activator.CreateInstance<T>()!;
                prop.SetValue(stub, value);
                list.Add(stub);
            }
            return list;
        }

        public static T CreateStub<T>(IEnumerable<(string propertyName, object value)> values)
        {
            var stub = Activator.CreateInstance<T>()!;
            var cache = new Dictionary<string, PropertyInfo>();

            foreach (var (propertyName, value) in values)
            {
                if (!cache.TryGetValue(propertyName, out var prop))
                {
                    prop = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
                           ?? throw new ArgumentException($"Property '{propertyName}' not found on {typeof(T).FullName}");
                    cache[propertyName] = prop;
                }

                prop.SetValue(stub, value);
            }
            return stub;
        }

        public static List<T> CreateStubs<T>(IEnumerable<IEnumerable<(string propertyName, object value)>> items)
        {
            var list = new List<T>();
            var cache = new Dictionary<string, PropertyInfo>();

            foreach (var item in items)
            {
                var stub = Activator.CreateInstance<T>()!;
                foreach (var (propertyName, value) in item)
                {
                    if (!cache.TryGetValue(propertyName, out var prop))
                    {
                        prop = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
                               ?? throw new ArgumentException($"Property '{propertyName}' not found on {typeof(T).FullName}");
                        cache[propertyName] = prop;
                    }

                    prop.SetValue(stub, value);
                }
                list.Add(stub);
            }
            return list;
        }
    }
}
