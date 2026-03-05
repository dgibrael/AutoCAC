namespace AutoCAC.Services;

public enum RPMSReport
{
    OrderDialog
}

public static class RPMSReportExtensions
{
    extension(RPMSReport value)
    {
        public string FullName => value switch
        {
            RPMSReport.OrderDialog => "Order Dialog Update App",
            _ => throw new Exception("menu option not found")
        };
        
    }
}