namespace AutoCAC.Extensions
{
    public static class StringExtensions
    {
        public static bool EndContains(this string data, string substring
            , StringComparison stringComparison = StringComparison.OrdinalIgnoreCase
            , int charsFromEnd = 10)
        {
            if (string.IsNullOrEmpty(data) || substring is null)
                return false;

            int strDiff = data.Length - substring.Length;
            if (strDiff < 0)
                return false;

            int searchStart = strDiff < charsFromEnd
                ? 0
                : data.Length - (charsFromEnd + substring.Length);

            ReadOnlySpan<char> span = data.AsSpan(searchStart);
            return span.IndexOf(substring.AsSpan(), stringComparison) >= 0;
        }
        public static bool EndContains(this string data, char character, int charsFromEnd = 30)
        {
            if (string.IsNullOrEmpty(data))
                return false;

            // If data is shorter than charsFromEnd, search the entire string
            int searchStart = data.Length < charsFromEnd
                ? 0
                : data.Length - charsFromEnd;

            ReadOnlySpan<char> span = data.AsSpan(searchStart);
            return span.IndexOf(character) >= 0;
        }
        public static bool ContainsIn(this string data, string substring,
            int startIndex = 0,
            int bufferLength = 30,
            StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            if (string.IsNullOrEmpty(data) || substring is null)
                return false;

            if (substring.Length > data.Length)
                return false;

            startIndex = Math.Clamp(startIndex, 0, data.Length - substring.Length);

            int maxLength = data.Length - startIndex;
            int spanLength = Math.Min(substring.Length + bufferLength, maxLength);

            ReadOnlySpan<char> span = data.AsSpan(startIndex, spanLength);
            return span.IndexOf(substring.AsSpan(), comparison) >= 0;
        }

        public static bool ContainsIn(this string data, char character,
            int startIndex = 0,
            int bufferLength = 30)
        {
            if (string.IsNullOrEmpty(data))
                return false;

            startIndex = Math.Clamp(startIndex, 0, data.Length - 1);

            int spanLength = Math.Min(bufferLength + 1, data.Length - startIndex); // +1 so even single char is included
            ReadOnlySpan<char> span = data.AsSpan(startIndex, spanLength);

            return span.IndexOf(character) >= 0;
        }
        public static int ReverseIndex(this string data, int endIndex)
        {
            if (string.IsNullOrEmpty(data))
                return 0;

            return Math.Max(data.Length - endIndex, 0);
        }


    }
}
