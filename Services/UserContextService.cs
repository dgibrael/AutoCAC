using AutoCAC.Extensions;
using AutoCAC.Models;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using System.DirectoryServices.AccountManagement;
using System.Security.Claims;

namespace AutoCAC.Services
{
    public class UserContextService
    {
        private readonly IDbContextFactory<mainContext> _dbFactory;
        private readonly AuthenticationStateProvider _authProvider;

        public AuthUser UserProfile { get; private set; }
        public ClaimsPrincipal CurrentUser { get; private set; }

        public string Username => CurrentUser?.Identity?.Name?.Replace("\\", "_").ToLower();

        public string DisplayName => $"{UserProfile?.FirstName} {UserProfile?.LastName}".Trim();

        public IEnumerable<AuthGroup> Groups =>
            UserProfile?.AuthUserGroups?.Select(ug => ug.Group) ?? Enumerable.Empty<AuthGroup>();

        public bool UserLoaded { get; private set; }
        private readonly SemaphoreSlim _initLock = new(1, 1);

        public bool IsInGroup(params string[] groupNames)
        {
            if (UserProfile == null || !UserProfile.IsActive)
                return false;

            if (groupNames == null || groupNames.Length == 0)
                return true; // Only check for active status

            return Groups.Any(g => groupNames.Contains(g.Name, StringComparer.OrdinalIgnoreCase));
        }

        public bool IsInGroupOrSuperuser(params string[] groupNames)
        {
            if (UserProfile == null || !UserProfile.IsActive)
                return false;
            
            if (UserProfile.IsSuperuser) return true;
            
            if (groupNames == null || groupNames.Length == 0)
                return false; // Only check for active status

            return Groups.Any(g => groupNames.Contains(g.Name, StringComparer.OrdinalIgnoreCase));
        }

        public bool IsClinical() => IsInGroupOrSuperuser("PharmacistSupervisor", "Pharmacist", "PublicHealth", "Nurse", "Provider"
            , "PharmacyTech", "PharmacyTechSupervisor", "PinonAdmin", "TsaileAdmin");
        public bool IsPharmacy() => IsInGroupOrSuperuser("PharmacistSupervisor", "Pharmacist", "PharmacyTech", "PharmacyTechSupervisor", "PinonAdmin", "TsaileAdmin");

        public UserContextService(
            IDbContextFactory<mainContext> dbFactory,
            AuthenticationStateProvider authProvider)
        {
            _dbFactory = dbFactory;
            _authProvider = authProvider;
        }

        public string AdLastName { get; set; }
        public string AdFirstName { get; set; }
        public string AdEmail { get; set; }
        public async Task EnsureInitializedAsync()
        {
            if (UserLoaded) return;

            await _initLock.WaitAsync();
            try
            {
                if (UserLoaded) return;

                var authState = await _authProvider.GetAuthenticationStateAsync();
                CurrentUser = authState.User;

                if (!(CurrentUser.Identity?.IsAuthenticated ?? false))
                {
                    UserLoaded = true;
                    return;
                }

                await using var db = await _dbFactory.CreateDbContextAsync();

                // Try to find existing profile
                UserProfile = await db.AuthUsers
                    .Include(u => u.AuthUserGroups).ThenInclude(ug => ug.Group)
                    .FirstOrDefaultAsync(u => u.Username.ToLower() == Username);
                GetPersonInfoFromClaimsOrAd();
                // If not found, create it
                if (UserProfile == null)
                {
                    var newUser = new AuthUser
                    {
                        Username = Username,
                        FirstName = AdFirstName ?? "",
                        LastName = AdLastName ?? "",
                        Email = AdEmail ?? "",
                        IsActive = true,
                        IsStaff = false,
                        IsSuperuser = false,
                        DateJoined = DateTimeOffset.Now,
                        // Placeholder; not used for authentication. Keep non-null to satisfy schema.
                        Password = "!"
                    };

                    db.AuthUsers.Add(newUser);

                    try
                    {
                        await db.SaveChangesAsync();
                    }
                    catch (DbUpdateException)
                    {
                        // Handle race: another request may have created it; re-query.
                        // Optionally inspect inner exception for unique key if you have one.
                    }

                    // Re-load with groups included
                    UserProfile = await db.AuthUsers
                        .Include(u => u.AuthUserGroups).ThenInclude(ug => ug.Group)
                        .FirstOrDefaultAsync(u => u.Username.ToLower() == Username);
                }
                else
                {
                    // Existing user — update any missing personal info
                    bool needsUpdate = false;

                    if (string.IsNullOrWhiteSpace(UserProfile.FirstName) && !string.IsNullOrWhiteSpace(AdFirstName))
                    {
                        UserProfile.FirstName = AdFirstName;
                        needsUpdate = true;
                    }

                    if (string.IsNullOrWhiteSpace(UserProfile.LastName) && !string.IsNullOrWhiteSpace(AdLastName))
                    {
                        UserProfile.LastName = AdLastName;
                        needsUpdate = true;
                    }

                    if (string.IsNullOrWhiteSpace(UserProfile.Email) && !string.IsNullOrWhiteSpace(AdEmail))
                    {
                        UserProfile.Email = AdEmail;
                        needsUpdate = true;
                    }

                    if (needsUpdate)
                    {
                        db.AuthUsers.Update(UserProfile);
                        await db.SaveChangesAsync();
                    }
                }

                UserLoaded = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        private void GetPersonInfoFromClaimsOrAd()
        {
            if (OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(Username))
            {
                try
                {
                    using var ctx = new PrincipalContext(ContextType.Domain);
                    using var up = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, Username.GetAfterLastDelimiter("_"));
                    if (up != null)
                    {
                        AdFirstName = string.IsNullOrWhiteSpace(up.GivenName) ? null : up.GivenName;
                        AdLastName = string.IsNullOrWhiteSpace(up.Surname) ? null : up.Surname;
                        AdEmail = string.IsNullOrWhiteSpace(up.EmailAddress) ? null : up.EmailAddress;
                        return;
                    }
                }
                catch
                {
                    // Ignore AD lookup failures (not domain-joined, permissions, etc.)
                }
            }
        }

        public bool CanAccess(string relativeUrl)
        {
            var path = NormalizeUrl(relativeUrl);

            return path switch
            {
                "/settings/stafflink/" => IsInGroupOrSuperuser("PharmacistSupervisor"),
                "/drugenteredit/" => IsInGroupOrSuperuser("PharmacistSupervisor"),
                "/benchmarkprice/" => IsInGroupOrSuperuser("PharmacistSupervisor"),
                "/inpatient/precisepk/" => IsInGroupOrSuperuser("PharmacistSupervisor", "Pharmacist", "PharmacyTech", "PharmacyTechSupervisor", "PinonAdmin", "TsaileAdmin"),
                "/inpatient/adt/" => IsClinical(),
                "/inpatient/medorder/" => IsClinical(),
                "/notes/" => IsClinical(),
                _ when path.StartsWith("/piverify/", StringComparison.OrdinalIgnoreCase)
                    => IsInGroupOrSuperuser("PharmacyTech", "Pharmacist", "PharamcytechSupervisor", "PharmacistSupervisor", "InsuranceVerifier"),
                _ when path.StartsWith("/supervisor/inpatient/", StringComparison.OrdinalIgnoreCase)
                    => IsInGroupOrSuperuser("PharmacistSupervisor"),
                _ => true // default: open to all active users
            };
        }

        private static string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "/";
            var u = url.Split('?', '#')[0];
            if (!u.StartsWith("/")) u = "/" + u;
            if (!u.EndsWith("/")) u = u + "/";

            return u.ToLowerInvariant();
        }


    }
}
