namespace AutoCAC.Utilities
{
    public static class AdGroupHelper
    {
        public static readonly Dictionary<string, string> Groups = new()
        {
            { "NAV/CHC CSU Staff", "All Chinle" },
            { "NAV/CHC Pharmacy", "All Pharmacy" },
            { "NAV/CHC Pharmacist", "Pharmacists" },
            { "NAV/CHC Emergency Department", "ED" },
            { "NAV/CHC Special Care Unit", "SCU" },
        };

        public static string Pharmacy => "NAV/CHC Pharmacy";
        public static string AllStaff => "NAV/CHC CSU Staff";
        public static string Pharmacist => "NAV/CHC Pharmacist";
        public static string Emergency => "NAV/CHC Emergency Department";
        public static string Scu => "NAV/CHC Special Care Unit";

        public static string GetLabel(string groupName) =>
            Groups.TryGetValue(groupName, out var label) ? label : null;
    }
}
