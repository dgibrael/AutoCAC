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
            StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            if (string.IsNullOrEmpty(data) || string.IsNullOrEmpty(value))
                return false;

            return data.LastLineSpan().IndexOf(value.AsSpan(), comparison) >= 0;
        }

        public static string LastLineContains(this string data, string[] values,
            StringComparison comparison = StringComparison.OrdinalIgnoreCase)
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


    }
}
