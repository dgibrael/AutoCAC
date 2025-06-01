using Newtonsoft.Json;
using System.Text;
using System;

namespace AutoCAC.Extensions
{
    public static class StringExtensions
    {
        public static int ReverseIndex(this string data, int endIndex)
        {
            return data.Length - endIndex;
        }
        public static int ReverseIndexSafe(this string data, int endIndex)
        {
            if (string.IsNullOrEmpty(data))
                return 0;

            return Math.Max(data.Length - endIndex, 0);
        }
        public static string LastLine(this string data)
        {
            if (string.IsNullOrEmpty(data))
                return string.Empty;

            ReadOnlySpan<char> span = data.AsSpan();
            int lastIndex = span.LastIndexOfAny('\r', '\n');

            return lastIndex >= 0
                ? span[(lastIndex + 1)..].ToString()
                : data;
        }
        public static ReadOnlySpan<char> LastLineSpan(this string data)
        {
            if (string.IsNullOrEmpty(data))
                return ReadOnlySpan<char>.Empty;

            ReadOnlySpan<char> span = data.AsSpan();
            int lastIndex = span.LastIndexOfAny('\r', '\n');

            return lastIndex >= 0
                ? span[(lastIndex + 1)..]
                : span;
        }
        public static bool LastLineContains(this string data, string value,
            StringComparison comparison = StringComparison.Ordinal)
        {
            if (string.IsNullOrEmpty(data) || string.IsNullOrEmpty(value))
                return false;

            return data.LastLineSpan().IndexOf(value.AsSpan(), comparison) >= 0;
        }

        public static string LastLineContains(this string data, string[] values,
            StringComparison comparison = StringComparison.Ordinal)
        {
            if (string.IsNullOrEmpty(data) || values == null || values.Length == 0)
                return null;

            ReadOnlySpan<char> line = data.LastLineSpan();

            foreach (var value in values)
            {
                if (string.IsNullOrEmpty(value)) continue;

                if (line.IndexOf(value.AsSpan(), comparison) >= 0)
                    return value;
            }

            return null;
        }

        public static string GetAfterLastDelimiter(this string data, string delimiter)
        {
            if (string.IsNullOrEmpty(data))
                return string.Empty;

            ReadOnlySpan<char> span = data.AsSpan();
            int lastIndex = span.LastIndexOf(delimiter.AsSpan());

            return lastIndex >= 0
                ? span[(lastIndex + 1)..].ToString()
                : data;
        }

        public static string GetBeforeLastDelimiter(this string data, string delimiter)
        {
            if (string.IsNullOrEmpty(data))
                return string.Empty;
            ReadOnlySpan<char> span = data.AsSpan();
            int lastIndex = span.LastIndexOf(delimiter.AsSpan());

            return lastIndex >= 0
                ? span[..lastIndex].ToString()
                : data;
        }

        public static string GetBeforeIdx(this string data, int lastIndex)
        {
            ReadOnlySpan<char> span = data.AsSpan();
            return lastIndex >= 0
                ? span[..lastIndex].ToString()
                : data;
        }

        public static string GetAfterIdx(this string data, int lastIndex)
        {
            ReadOnlySpan<char> span = data.AsSpan();
            return lastIndex >= 0
                ? span[(lastIndex + 1)..].ToString()
                : data;
        }

        public static IEnumerable<T> JsonStrToObject<T>(this string json)
        {
            return JsonConvert.DeserializeObject<IEnumerable<T>>(json) ?? new List<T>();
        }

        public static IEnumerable<IDictionary<string, object>> JsonStrToObject(this string json)
        {
            return JsonConvert.DeserializeObject<IEnumerable<IDictionary<string, object>>>(json);
        }

        public static string JsonStrFromReport(this string data)
        {
            if (string.IsNullOrEmpty(data))
                return "";

            var span = data.AsSpan();

            int lastClose = span.LastIndexOf('}');
            if (lastClose < 0) return "";

            int firstOpen = span.IndexOf('{');
            if (firstOpen < 0) return "";

            var trimmedSpan = span[firstOpen..(lastClose + 1)];

            var builder = new StringBuilder();
            builder.Append('[');
            builder.Append(trimmedSpan);
            builder.Append(']');
            builder
                .Replace('"', '\'')
                .Replace('\x1F', '"')
                .Replace("\f", "")
                .Replace("\\", "&#92")
                .Replace("\n", "")
                .Replace("\r", "");

            return builder.ToString();
        }
    }
}
