using System;

namespace AutoCAC.Utilities;

public static class PeriodOptionExtensions
{
    public static DateOnly CurrentStart(this PeriodOption preset, DateOnly? today = null)
    {
        var t = today ?? DateOnly.FromDateTime(DateTime.Now);

        return preset switch
        {
            PeriodOption.Past30Days => t.AddDays(-30),
            PeriodOption.Past60Days => t.AddDays(-60),
            PeriodOption.Past90Days => t.AddDays(-90),
            PeriodOption.Past365Days => t.AddDays(-365),

            PeriodOption.LastMonth => StartOfMonth(t).AddMonths(-1),
            PeriodOption.LastQuarter => StartOfQuarter(t).AddMonths(-3),

            PeriodOption.LastYear => new DateOnly(t.Year - 1, 1, 1),
            PeriodOption.YearToDate => new DateOnly(t.Year, 1, 1),

            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null)
        };
    }

    // ✅ inclusive end date
    public static DateOnly CurrentEnd(this PeriodOption preset, DateOnly? today = null)
    {
        var t = today ?? DateOnly.FromDateTime(DateTime.Now);

        return preset switch
        {
            PeriodOption.Past30Days or PeriodOption.Past60Days or
            PeriodOption.Past90Days or PeriodOption.Past365Days => t,

            PeriodOption.LastMonth => StartOfMonth(t).AddDays(-1),
            PeriodOption.LastQuarter => StartOfQuarter(t).AddDays(-1),
            PeriodOption.LastYear => new DateOnly(t.Year - 1, 12, 31),

            PeriodOption.YearToDate => t,

            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null)
        };
    }

    public static DateOnly CompareStart(this PeriodOption preset, DateOnly? today = null)
    {
        var t = today ?? DateOnly.FromDateTime(DateTime.Now);

        return preset switch
        {
            PeriodOption.Past30Days => preset.CurrentStart(t).AddDays(-30),
            PeriodOption.Past60Days => preset.CurrentStart(t).AddDays(-60),
            PeriodOption.Past90Days => preset.CurrentStart(t).AddDays(-90),
            PeriodOption.Past365Days => preset.CurrentStart(t).AddDays(-365),

            PeriodOption.LastMonth => preset.CurrentStart(t).AddMonths(-1),
            PeriodOption.LastQuarter => preset.CurrentStart(t).AddMonths(-3),
            PeriodOption.LastYear => preset.CurrentStart(t).AddYears(-1),

            PeriodOption.YearToDate => new DateOnly(t.Year - 1, 1, 1),

            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null)
        };
    }

    // ✅ inclusive end date for comparison period too
    public static DateOnly CompareEnd(this PeriodOption preset, DateOnly? today = null)
    {
        var t = today ?? DateOnly.FromDateTime(DateTime.Now);

        return preset switch
        {
            PeriodOption.Past30Days or PeriodOption.Past60Days or
            PeriodOption.Past90Days or PeriodOption.Past365Days
                => preset.CurrentStart(t).AddDays(-1),

            PeriodOption.LastMonth => preset.CurrentEnd(t).AddMonths(-1),
            PeriodOption.LastQuarter => preset.CurrentEnd(t).AddMonths(-3),
            PeriodOption.LastYear => preset.CurrentEnd(t).AddYears(-1),

            // Prior year through the same day-of-year as today (inclusive)
            PeriodOption.YearToDate => t.AddYears(-1),

            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null)
        };
    }

    private static DateOnly StartOfMonth(DateOnly d) => new DateOnly(d.Year, d.Month, 1);

    private static DateOnly StartOfQuarter(DateOnly d)
    {
        var startMonth = ((d.Month - 1) / 3) * 3 + 1; // 1,4,7,10
        return new DateOnly(d.Year, startMonth, 1);
    }
}