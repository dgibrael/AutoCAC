using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;

namespace AutoCAC.Extensions;

public static class EnumExtensionsCustom
{
    extension<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        public bool IsFirst
            => EqualityComparer<TEnum>.Default.Equals(
                EnumCache<TEnum>.Values[0], value);

        public bool IsLast
            => EqualityComparer<TEnum>.Default.Equals(
                EnumCache<TEnum>.Values[^1], value);

        public TEnum Next()
        {
            var values = EnumCache<TEnum>.Values;

            if (value.IsLast)
                return value;

            var index = Array.IndexOf(values, value);
            return values[index + 1];
        }

        public TEnum Previous()
        {
            var values = EnumCache<TEnum>.Values;

            if (value.IsFirst)
                return value;

            var index = Array.IndexOf(values, value);
            return values[index - 1];
        }

        public string GetDisplayName()
            => EnumCache<TEnum>.DisplayNames[Array.IndexOf(EnumCache<TEnum>.Values, value)];
    }

    public static IReadOnlyDictionary<TEnum, string> GetDictionary<TEnum>()
        where TEnum : struct, Enum
        => EnumCache<TEnum>.Dictionary;

    private static class EnumCache<TEnum>
        where TEnum : struct, Enum
    {
        public static readonly TEnum[] Values =
            (TEnum[])Enum.GetValues(typeof(TEnum));

        public static readonly string[] DisplayNames = BuildDisplayNames();

        public static readonly IReadOnlyDictionary<TEnum, string> Dictionary = BuildDictionary();

        static string[] BuildDisplayNames()
        {
            var type = typeof(TEnum);
            var names = Enum.GetNames(type); // same order as Values
            var result = new string[names.Length];

            for (int i = 0; i < names.Length; i++)
            {
                var field = type.GetField(names[i], BindingFlags.Public | BindingFlags.Static);

                var display = field?.GetCustomAttribute<DisplayAttribute>();
                var text = display?.GetName(); // supports ResourceType localization

                result[i] = string.IsNullOrWhiteSpace(text)
                    ? SplitPascalCase(names[i])
                    : text;
            }

            return result;
        }

        static IReadOnlyDictionary<TEnum, string> BuildDictionary()
        {
            var dict = new Dictionary<TEnum, string>(Values.Length);
            for (int i = 0; i < Values.Length; i++)
                dict[Values[i]] = DisplayNames[i];
            return dict;
        }

        static string SplitPascalCase(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var sb = new StringBuilder(input.Length + 8);
            sb.Append(input[0]);

            for (int i = 1; i < input.Length; i++)
            {
                var c = input[i];
                var prev = input[i - 1];

                var boundary =
                    // lower -> upper (pastDays -> past Days)
                    (char.IsUpper(c) && char.IsLower(prev)) ||

                    // letter -> digit (Past30 -> Past 30)
                    (char.IsDigit(c) && char.IsLetter(prev)) ||

                    // digit -> letter (30Days -> 30 Days)
                    (char.IsLetter(c) && char.IsDigit(prev)) ||

                    // acronym end: "ISOCode" -> "ISO Code" (O -> C where next is lower)
                    (char.IsUpper(c) && char.IsUpper(prev) &&
                     i + 1 < input.Length && char.IsLower(input[i + 1]));

                if (boundary)
                    sb.Append(' ');

                sb.Append(c);
            }

            return sb.ToString();
        }
    }
}
