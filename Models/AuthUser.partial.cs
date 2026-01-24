namespace AutoCAC.Models;

public partial class AuthUser
{
    public string DisplayName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(FirstName) || string.IsNullOrWhiteSpace(LastName))
                return Username;

            return $"{LastName}, {FirstName}";
        }
    }
}
