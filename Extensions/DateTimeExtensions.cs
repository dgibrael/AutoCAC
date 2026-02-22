namespace AutoCAC.Extensions;

public static class DateTimeExtensions
{
    extension(DateTime value)
    {
        public DateOnly DateOnly
            => DateOnly.FromDateTime(value);
    }
}
