namespace AutoCAC.Services;

public sealed class LeaveWindow
{
    public int WindowNumber { get; set; }

    public DateTime? WindowStart { get; set; }
    public DateTime? WindowEnd { get; set; }

    public DateOnly LeaveStartDate { get; set; }
    public DateOnly LeaveEndDate { get; set; }
    public bool IsInWindow(DateTime? current = null)
    {
        DateTime now = current ?? DateTime.Now;
        if (WindowNumber == 0 || WindowStart == null || WindowEnd == null) return false;
        DateTime start = WindowStart.Value;
        DateTime end = WindowEnd.Value;
        return now >= start && now <= end;
    }
}