public enum InsuranceReportType
{
    All,
    AllActive,
    Expired,
    Expiring
};

public static class InsuranceReportTypeExtensions
{
    extension(InsuranceReportType value)
    {
        public string ReportDescription => value switch
        {
            InsuranceReportType.All => "All Insurance Polices",
            InsuranceReportType.AllActive => "All Insurance Polices with no expiration or expiration in the future",
            InsuranceReportType.Expired => "Insurance polices expired in past 365 days",
            InsuranceReportType.Expiring => "Insurance polices expiring within the next 30 days",
            _ => "All Insurance Policies"
        };
    }
}
