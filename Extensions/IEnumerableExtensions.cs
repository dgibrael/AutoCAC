using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace AutoCAC.Extensions
{
    public static class IEnumerableExtensions
    {
        public static List<Dictionary<string, object?>> ToDictionaryList<T>(this IEnumerable<T> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var result = new List<Dictionary<string, object?>>();
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var item in source)
            {
                var dict = new Dictionary<string, object?>();

                foreach (var prop in props)
                {
                    dict[prop.Name] = prop.GetValue(item);
                }

                result.Add(dict);
            }

            return result;
        }
        public static StringContent ToExcelExportContent<T>(this IEnumerable<T> source)
        {
            var dictList = source.ToDictionaryList();
            var json = JsonSerializer.Serialize(dictList);
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

    }
}
