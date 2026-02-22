using AutoCAC.Extensions;
namespace AutoCAC.Utilities;
public sealed class PeriodSelection
{
    private PeriodOption _selectedPeriod;
    private DateOnly _today;
    private DateOnly? _customStart;
    private DateOnly? _customEnd;
    public PeriodSelection(PeriodOption selectedPeriod, DateOnly? today = null, DateTime? customStart = null, DateTime? customEnd = null)
    {
        _today = today ?? DateTime.Now.DateOnly;
        _selectedPeriod = selectedPeriod;
        Refresh();
    }

    public DateOnly CustomEnd
    {
        get => _customEnd ?? DateTime.Now.DateOnly;
        set
        {
            _customEnd = value;
            if (CustomStart > value)
            {
                _customStart = value;
            }
            RefreshNow();
        }
    }
    public DateOnly CustomStart
    {
        get => _customStart ?? CustomEnd.AddDays(-30);
        set
        {
            _customStart = value;
            if (CustomEnd < value) 
            { 
                _customEnd = value;
            }
            RefreshNow();
        }
    }

    public PeriodOption SelectedOption
    {
        get => _selectedPeriod;
        set
        {
            if (_selectedPeriod == value) return;
            _selectedPeriod = value;
            RefreshNow();
        }
    }

    public DateOnly CurrentStart { get; private set; }
    public DateOnly CurrentEnd { get; private set; }
    public DateOnly CompareStart { get; private set; }
    public DateOnly CompareEnd { get; private set; }

    public void Refresh()
    {
        if (SelectedOption == PeriodOption.Custom)
        {
            CurrentStart = CustomStart;
            CurrentEnd = CustomEnd;
            CompareEnd = CurrentStart.AddDays(-1);
            var offset = CurrentEnd.DayNumber - CurrentStart.DayNumber;
            CompareStart = CompareEnd.AddDays(-offset);
            return;
        }
        // Uses your extension methods as-is
        CurrentStart = SelectedOption.CurrentStart(_today);
        CurrentEnd = SelectedOption.CurrentEnd(_today);
        CompareStart = SelectedOption.CompareStart(_today);
        CompareEnd = SelectedOption.CompareEnd(_today);
    }

    // convenience if you want “refresh with latest clock”
    public void RefreshNow()
    {
        _today = DateTime.Now.DateOnly;
        Refresh();
    }
}
