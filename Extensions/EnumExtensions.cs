namespace AutoCAC.Extensions;

public static class EnumExtensions
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
    }

    private static class EnumCache<TEnum>
        where TEnum : struct, Enum
    {
        public static readonly TEnum[] Values =
            (TEnum[])Enum.GetValues(typeof(TEnum));
    }
}
