namespace AutoCAC.Services;

public sealed class LeaveWindowOptions
{
    public DateOnly StartDate { get; set; }
    public int CycleDays { get; set; }
    public int LeaveEndOffsetDays { get; set; }
    public int Window1StartOffsetDays { get; set; }
    public int Window1LengthDays { get; set; }
    public int Window2GapAfterWindow1Days { get; set; }
    public int Window2LengthDays { get; set; }
    public TimeOnly WindowOpenTime { get; set; }
    public TimeOnly WindowCloseTime { get; set; }
}