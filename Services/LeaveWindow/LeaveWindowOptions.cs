namespace AutoCAC.Services;

public sealed class LeaveWindowOptions
{
    public DateOnly FirstWindow1StartDate { get; set; }

    public int Window1EndOffsetDays { get; set; }

    public int Window2StartOffsetDays { get; set; }
    public int Window2EndOffsetDays { get; set; }

    public int LeaveStartOffsetDays { get; set; }
    public int LeaveEndOffsetDays { get; set; }

    public TimeOnly WindowOpenTime { get; set; }
    public TimeOnly WindowCloseTime { get; set; }

    public int CycleDays =>
        LeaveEndOffsetDays - LeaveStartOffsetDays + 1;
}