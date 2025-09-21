using Microsoft.JSInterop;
using System.Reflection;
using System.Text;

namespace AutoCAC.Extensions
{
    public static class CsvStringExtensions
    {
        public static string ToCsvString<T>(
            this IEnumerable<T> source,
            IEnumerable<string> includeProperties = null
        )
        {
            var allProps = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                    .Where(p => p.GetIndexParameters().Length == 0);

            var props = (includeProperties == null || !includeProperties.Any())
                ? allProps.ToArray()   // all columns
                : allProps.Where(p => includeProperties.Contains(p.Name,
                            StringComparer.OrdinalIgnoreCase)).ToArray();

            var sb = new StringBuilder();

            // header
            WriteLine(sb, props.Select(p => p.Name));

            // rows
            foreach (var item in source)
                WriteLine(sb, props.Select(p => ToCsvValue(p.GetValue(item))));

            return sb.ToString();

            static void WriteLine(StringBuilder sb, IEnumerable<string?> values)
            {
                bool first = true;
                foreach (var v in values)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append(Escape(v ?? string.Empty));
                }
                sb.Append("\r\n"); // Excel-friendly
            }

            static string Escape(string s) =>
                (s.Contains('"') || s.Contains(',') || s.Contains('\r') || s.Contains('\n'))
                ? $"\"{s.Replace("\"", "\"\"")}\"" : s;

            static string ToCsvValue(object? v) => v switch
            {
                null => string.Empty,
                DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss"),
                DateOnly d => d.ToString("yyyy-MM-dd"),
                TimeOnly t => t.ToString("HH:mm:ss"),
                bool b => b ? "TRUE" : "FALSE",
                IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
                _ => v?.ToString() ?? string.Empty
            };
        }
        public static Task DownloadAsCsvAsync<T>(
            this IEnumerable<T> source,
            IJSRuntime js,
            string fileName = "data.csv",
            IEnumerable<string> includeProperties = null
            )
        {
            var csv = source.ToCsvString(includeProperties);
            // CSV: supply CSV MIME and add BOM for best Excel compatibility
            return js.DownloadString(csv, fileName, "text/csv;charset=utf-8", addUtf8Bom: true);
        }
    }

}
