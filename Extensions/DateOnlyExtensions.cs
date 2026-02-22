namespace AutoCAC.Extensions;

public static class DateOnlyExtensions
{
    extension(DateOnly value)
    {
        public DateTime StartOfDay
            => value.ToDateTime(TimeOnly.MinValue);

        public DateTime EndOfDay
            => value.ToDateTime(TimeOnly.MaxValue);

        public DateTime StartOfNextDay
            => value.AddDays(1).ToDateTime(TimeOnly.MinValue);
    }
}
