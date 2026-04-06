using AutoCAC.Extensions;
using Microsoft.EntityFrameworkCore;

namespace AutoCAC.Models;

public sealed record AdUserDto(
    string SamAccountName,
    string UserPrincipalName,
    string DisplayName,
    bool Enabled,
    string Email = null
)
{
    public string D1Username => $"d1_{SamAccountName}";
    public string FullName => DisplayName?.GetBeforeLastDelimiter("(");
    public string LastName => FullName?.GetBeforeLastDelimiter(",");
    public string FirstName => FullName?.GetAfterLastDelimiter(",");
    public AuthUser ToNewAuthUser()
    {
        return new AuthUser
        {
            Username = D1Username,
            FirstName = FirstName?.Trim() ?? "",
            LastName = LastName?.Trim() ?? "",
            Email = Email?.Trim() ?? "",
            IsActive = true,
            IsStaff = false,
            IsSuperuser = false,
            DateJoined = DateTimeOffset.Now,
            // Placeholder; not used for authentication. Keep non-null to satisfy schema.
            Password = "!"
        };
    }

    public bool ModifyAuthUser(AuthUser authUser)
    {
        var changed = false;

        if (string.IsNullOrWhiteSpace(authUser.FirstName) && !string.IsNullOrWhiteSpace(FirstName))
        {
            authUser.FirstName = FirstName.Trim();
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(authUser.LastName) && !string.IsNullOrWhiteSpace(LastName))
        {
            authUser.LastName = LastName.Trim();
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(authUser.Email) && !string.IsNullOrWhiteSpace(Email))
        {
            authUser.Email = Email.Trim();
            changed = true;
        }

        if (!authUser.IsActive && Enabled)
        {
            authUser.IsActive = true;
            changed = true;
        }

        return changed;
    }
}