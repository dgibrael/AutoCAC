namespace AutoCAC.Extensions
{
    public static class NullableIntExtensions
    {
        public static bool NotNullOrZero(this int? value) => value.HasValue && value.Value != 0;
    }
}
