namespace AutoCAC.Services;

internal sealed class LeaveWindowCycle
{
    public DateOnly CycleStartDate { get; set; }
    public DateOnly LeaveEndDate { get; set; }
    public DateTime Window1Start { get; set; }
    public DateTime Window1End { get; set; }
    public DateTime Window2Start { get; set; }
    public DateTime Window2End { get; set; }
}