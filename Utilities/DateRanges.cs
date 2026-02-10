namespace AutoCAC.Utilities;
public static class DateRanges
{
    public static (DateOnly Start, DateOnly End) LastQuarter()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        var currentQuarter = ((today.Month - 1) / 3) + 1; // 1..4

        var lastQuarter = currentQuarter - 1;
        var year = today.Year;
        if (lastQuarter == 0)
        {
            lastQuarter = 4;
            year--;
        }

        var startMonth = (lastQuarter - 1) * 3 + 1; // 1,4,7,10
        var start = new DateOnly(year, startMonth, 1);
        var end = start.AddMonths(3).AddDays(-1);

        return (start, end);
    }
}
