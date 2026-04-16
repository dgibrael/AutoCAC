namespace AutoCAC.Models;

public enum TaskScheduleType
{
    Daily,
    Weekly,
    Monthly,
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
                    if (!IntervalValue.HasValue || IntervalValue.Value < 1 || IntervalValue.Value > 31)
                        throw new InvalidOperationException("Monthly schedule requires IntervalValue between 1 and 31.");

                    var day = Math.Min(IntervalValue.Value, DateTime.DaysInMonth(current.Year, current.Month));
                    var candidate = new DateTime(current.Year, current.Month, day) + time;

                    if (candidate > current)
                        return candidate;

                    var nextMonth = current.AddMonths(1);
                    day = Math.Min(IntervalValue.Value, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
                    return new DateTime(nextMonth.Year, nextMonth.Month, day) + time;
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