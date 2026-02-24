namespace AutoCAC.Extensions;

public static class DateOnlyExtensions
{
    public static int GetAge(this DateOnly dateOfBirth, DateOnly? today = null)
    {
        var now = today ?? DateOnly.FromDateTime(DateTime.Today);

        int age = now.Year - dateOfBirth.Year;

        var birthdayThisYear = dateOfBirth.AddYears(age);
        if (birthdayThisYear > now)
            age--;

        if (age < 0) age = 0;
        return age;
    }

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
