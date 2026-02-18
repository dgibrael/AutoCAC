namespace AutoCAC.Utilities;
public static class PeriodOptionExtensions
{
    public static DateTime CurrentStart(this PeriodOption preset, DateTime? now = null)
    {
        var n = now ?? DateTime.Now;

        return preset switch
        {
            PeriodOption.Past30Days => n.AddDays(-30),
            PeriodOption.Past60Days => n.AddDays(-60),
            PeriodOption.Past90Days => n.AddDays(-90),
            PeriodOption.Past365Days => n.AddDays(-365),
            PeriodOption.LastMonth => StartOfMonth(n).AddMonths(-1),
            PeriodOption.LastQuarter => StartOfQuarter(n).AddMonths(-3),
            PeriodOption.LastYear => new DateTime(n.Year - 1, 1, 1),
            PeriodOption.YearToDate => new DateTime(n.Year, 1, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null)
        };
    }

    public static DateTime CurrentEnd(this PeriodOption preset, DateTime? now = null)
    {
        var n = now ?? DateTime.Now;

        return preset switch
        {
            PeriodOption.Past30Days or PeriodOption.Past60Days or
            PeriodOption.Past90Days or PeriodOption.Past365Days => n,

            PeriodOption.LastMonth => StartOfMonth(n),
            PeriodOption.LastQuarter => StartOfQuarter(n),
            PeriodOption.LastYear => new DateTime(n.Year, 1, 1),
            PeriodOption.YearToDate => n,
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null)
        };
    }

    public static DateTime CompareStart(this PeriodOption preset, DateTime? now = null)
    {
        var n = now ?? DateTime.Now;

        return preset switch
        {
            PeriodOption.Past30Days => CurrentStart(preset, n).AddDays(-30),
            PeriodOption.Past60Days => CurrentStart(preset, n).AddDays(-60),
            PeriodOption.Past90Days => CurrentStart(preset, n).AddDays(-90),
            PeriodOption.Past365Days => CurrentStart(preset, n).AddDays(-365),
            PeriodOption.LastMonth => CurrentStart(preset, n).AddMonths(-1),
            PeriodOption.LastQuarter => CurrentStart(preset, n).AddMonths(-3),
            PeriodOption.LastYear => CurrentStart(preset, n).AddYears(-1),

            PeriodOption.YearToDate => new DateTime(n.Year - 1, 1, 1),

            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null)
        };
    }

    public static DateTime CompareEnd(this PeriodOption preset, DateTime? now = null)
    {
        var n = now ?? DateTime.Now;

        return preset switch
        {
            PeriodOption.Past30Days or PeriodOption.Past60Days or
            PeriodOption.Past90Days or PeriodOption.Past365Days
            => CurrentStart(preset, n),

            PeriodOption.LastMonth => CurrentEnd(preset, n).AddMonths(-1),
            PeriodOption.LastQuarter => CurrentEnd(preset, n).AddMonths(-3),
            PeriodOption.LastYear => CurrentEnd(preset, n).AddYears(-1),

            PeriodOption.YearToDate => n.AddYears(-1),

            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null)
        };
    }

    private static DateTime StartOfMonth(DateTime dt) => new DateTime(dt.Year, dt.Month, 1);

    private static DateTime StartOfQuarter(DateTime dt)
    {
        var startMonth = ((dt.Month - 1) / 3) * 3 + 1; // 1,4,7,10
        return new DateTime(dt.Year, startMonth, 1);
    }
}
