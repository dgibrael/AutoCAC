using AutoCAC.Extensions;
using AutoCAC.Utilities;

namespace AutoCAC.Models;

public enum TaskScheduleType
{
    Daily,
    Weekly,
    Monthly,
    Quarterly,
    EveryXMinutes
}

public partial class ScheduledTaskSchedule
{
    public DateTime GetNextRunAt(DateTime? now = null)
    {
        var current = now ?? DateTime.Now;
        var time = (TimeOfDay ?? new TimeOnly(6, 0)).ToTimeSpan();
        var scheduleEnum = Enum.Parse<TaskScheduleType>(ScheduleType, ignoreCase: true);
        switch (scheduleEnum)
        {
            case TaskScheduleType.Daily:
                {
                    var candidate = current.Date + time;
                    return candidate > current ? candidate : candidate.AddDays(1);
                }

            case TaskScheduleType.Weekly:
                {
                    if (!IntervalValue.HasValue || IntervalValue.Value < 0 || IntervalValue.Value > 6)
                        throw new InvalidOperationException("Weekly schedule requires IntervalValue between 0 and 6.");

                    var offset = (IntervalValue.Value - (int)current.DayOfWeek + 7) % 7;
                    var candidate = current.Date.AddDays(offset) + time;

                    return candidate > current ? candidate : candidate.AddDays(7);
                }

            case TaskScheduleType.Monthly:
                {
                    var daysInMonth = DateTime.DaysInMonth(current.Year, current.Month);
                    int day = 1;
                    if (IntervalValue == null)
                    {
                        day = 1;
                    }
                    else if (IntervalValue > daysInMonth)
                    {
                        day = daysInMonth;
                    }
                    else if (IntervalValue <= 0)
                    {
                        day = daysInMonth - IntervalValue.Value + 1;
                    }
                    else
                    {
                        day = IntervalValue.Value;
                    }
                    var candidate = new DateTime(current.Year, current.Month, day) + time;

                    if (candidate > current)
                        return candidate;

                    var nextMonth = current.AddMonths(1);
                    daysInMonth = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
                    if (IntervalValue == null)
                    {
                        day = 1;
                    }
                    else if (IntervalValue > daysInMonth)
                    {
                        day = daysInMonth;
                    }
                    else if (IntervalValue <= 0)
                    {
                        day = daysInMonth - IntervalValue.Value + 1;
                    }
                    return new DateTime(nextMonth.Year, nextMonth.Month, day) + time;
                }
            case TaskScheduleType.Quarterly:
                {
                    DateTime GetQuarterStart(DateTime date)
                    {
                        var quarterStartMonth = ((date.Month - 1) / 3) * 3 + 1;
                        return new DateTime(date.Year, quarterStartMonth, 1);
                    }

                    int ResolveDayOfQuarter(DateTime quarterStart)
                    {
                        var quarterEnd = quarterStart.AddMonths(3).AddDays(-1);
                        var daysInQuarter = (quarterEnd - quarterStart).Days + 1;

                        if (IntervalValue == null)
                            return 1;

                        if (IntervalValue > daysInQuarter)
                            return daysInQuarter;

                        if (IntervalValue <= 0)
                            return daysInQuarter + IntervalValue.Value + 1;

                        return IntervalValue.Value;
                    }

                    var quarterStart = GetQuarterStart(current);
                    var dayOfQuarter = ResolveDayOfQuarter(quarterStart);
                    var candidate = quarterStart.AddDays(dayOfQuarter - 1).Date + time;

                    if (candidate > current)
                        return candidate;

                    var nextQuarterStart = quarterStart.AddMonths(3);
                    dayOfQuarter = ResolveDayOfQuarter(nextQuarterStart);

                    return nextQuarterStart.AddDays(dayOfQuarter - 1).Date + time;
                }

            case TaskScheduleType.EveryXMinutes:
                {
                    if (!IntervalValue.HasValue || IntervalValue.Value < 1)
                        throw new InvalidOperationException("EveryXMinutes schedule requires IntervalValue >= 1.");

                    return current.AddMinutes(IntervalValue.Value);
                }

            default:
                throw new InvalidOperationException($"Unknown schedule type '{ScheduleType}'.");
        }
    }
}