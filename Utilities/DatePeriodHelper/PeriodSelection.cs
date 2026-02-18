namespace AutoCAC.Utilities;
public sealed class PeriodSelection
{
    private PeriodOption _selectedPeriod;
    private DateTime _now;

    public PeriodSelection(PeriodOption selectedPeriod, DateTime? now = null)
    {
        _now = now ?? DateTime.Now;
        _selectedPeriod = selectedPeriod;
        Refresh();
    }

    public PeriodOption SelectedOption
    {
        get => _selectedPeriod;
        set
        {
            if (_selectedPeriod == value) return;
            _selectedPeriod = value;
            Refresh();
        }
    }

    // Bindable too if you ever want it, otherwise keep it set-only via method
    public DateTime Now
    {
        get => _now;
        set
        {
            if (_now == value) return;
            _now = value;
            Refresh();
        }
    }

    public DateTime CurrentStart { get; private set; }
    public DateTime CurrentEnd { get; private set; }
    public DateTime CompareStart { get; private set; }
    public DateTime CompareEnd { get; private set; }

    public void Refresh()
    {
        // Uses your extension methods as-is
        CurrentStart = SelectedOption.CurrentStart(_now);
        CurrentEnd = SelectedOption.CurrentEnd(_now);
        CompareStart = SelectedOption.CompareStart(_now);
        CompareEnd = SelectedOption.CompareEnd(_now);
    }

    // convenience if you want “refresh with latest clock”
    public void RefreshNow()
    {
        _now = DateTime.Now;
        Refresh();
    }
}
