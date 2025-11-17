namespace AutoCAC.Models;

public sealed record AdUserDto(
    string SamAccountName,
    string UserPrincipalName,
    string DisplayName,
    bool Enabled,
    string Email = null
);
