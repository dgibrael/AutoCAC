using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace AutoCAC.Extensions
{
    public static class IEnumerableExtensions
    {
        public static List<Dictionary<string, object>> ToDictionaryList<T>(this IEnumerable<T> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var result = new List<Dictionary<string, object>>();
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var item in source)
            {
                var dict = new Dictionary<string, object>();

                foreach (var prop in props)
                {
                    dict[prop.Name] = prop.GetValue(item);
                }

                result.Add(dict);
            }

            return result;
        }

        public static StringContent FormatForExcelFromDict(this IEnumerable<IDictionary<string, object>> source)
        {
            var json = JsonSerializer.Serialize(source);
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        public static StringContent FormatForExcelFromObject<T>(this IEnumerable<T> source)
        {
            var dictList = source.ToDictionaryList();
            var json = JsonSerializer.Serialize(dictList);
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        public static void TrimBetween(
            this List<string> list,
            string startString = null,
            string endString = null,
            StringComparison startComparison = StringComparison.OrdinalIgnoreCase,
            StringComparison endComparison = StringComparison.OrdinalIgnoreCase)
        {
            if (list == null || list.Count == 0) return;

            // Handle start trimming
            if (!string.IsNullOrEmpty(startString))
            {
                int startIndex = list.FindIndex(s => s.Contains(startString, startComparison));
                if (startIndex != -1)
                {
                    list.RemoveRange(0, startIndex+1);
                }
            }

            // Handle end trimming (after possibly modified list)
            if (!string.IsNullOrEmpty(endString))
            {
                int endIndex = list.FindLastIndex(s => s.Contains(endString, endComparison));
                if (endIndex != -1)
                {
                    list.RemoveRange(endIndex, list.Count - endIndex);
                }
            }
        }


    }
}
