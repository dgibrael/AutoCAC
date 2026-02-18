namespace AutoCAC.Utilities;

public static class HelperMethods
{
    public static string ConditionalTextCss(bool IsBad = false, bool IsGood = false, bool IsWarning = false)
    {
        if (IsBad) return "rz-color-danger";
        if (IsGood) return "rz-color-success";
        if (IsWarning) return "rz-color-warning";
        return "";
    }
}