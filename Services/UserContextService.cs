using AutoCAC.Extensions;
using AutoCAC.Models;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Security.Claims;

namespace AutoCAC.Services
{
    public class UserContextService
    {
        private readonly IDbContextFactory<mainContext> _dbFactory;
        private readonly AuthenticationStateProvider _authProvider;

        public AuthUser UserProfile { get; private set; }
        public ClaimsPrincipal CurrentUser { get; private set; }
        public HashSet<string> AdGroups { get; private set; } =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public string Username { get; private set; }

        public string DisplayName => $"{UserProfile?.FirstName} {UserProfile?.LastName}".Trim();

        public IEnumerable<AuthGroup> DbGroups =>
            UserProfile?.AuthUserGroups?.Select(ug => ug.Group) ?? Enumerable.Empty<AuthGroup>();
        public List<RestrictedPage> AllowedRestrictedPages { get; private set; } = new();
        public List<RestrictedPage> AllRestrictedPages { get; private set; } = new();
        public bool UserLoaded { get; private set; }
        private readonly SemaphoreSlim _initLock = new(1, 1);

        public async Task LoadAllowedPages()
        {
            if (!UserLoaded) return;

            await using var db = await _dbFactory.CreateDbContextAsync();
            AllRestrictedPages = await db.RestrictedPages
                                    .Include(x => x.RestrictedPageGroups)
                                        .ThenInclude(x => x.AuthGroup)
                                    .ToListAsync();
            var userGroupIds = new HashSet<int>(
                DbGroups.Select(g => g.Id)
            );
            AllowedRestrictedPages = AllRestrictedPages
                                    .Where(x => (UserProfile.IsSuperuser && x.AlwaysAllowSuperusers) 
                                        || x.RestrictedPageGroups.Any(y => userGroupIds.Contains(y.AuthGroupId))
                                    )
                                    .ToList();
        }

        public bool IsInGroup(params string[] groupNames)
        {
            if (UserProfile == null || !UserProfile.IsActive)
                return false;

            if (groupNames == null || groupNames.Length == 0)
                return true; // Only check for active status

            return DbGroups.Any(g => groupNames.Contains(g.Name, StringComparer.OrdinalIgnoreCase));
        }

        public bool IsInGroupOrSuperuser(params string[] groupNames)
        {
            if (UserProfile == null || !UserProfile.IsActive)
                return false;
            
            if (UserProfile.IsSuperuser) return true;
            
            if (groupNames == null || groupNames.Length == 0)
                return false; // Only check for active status

            return DbGroups.Any(g => groupNames.Contains(g.Name, StringComparer.OrdinalIgnoreCase));
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
                Username = CurrentUser?.Identity?.Name?.Replace("\\", "_")?.ToLower();
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
                if (AdGroups.Count > 0)
                {
                    // current groups the user already has
                    var userGroupIds = new HashSet<int>(
                        DbGroups.Select(g => g.Id)
                    );

                    var autoMatchGroups = await db.GroupAutoMatches
                        .Include(x => x.AuthGroup)
                        .Where(x => AdGroups.Contains(x.AdGroup))           // AD group match
                        .Where(x => !userGroupIds.Contains(x.AuthGroupId))  // not already in DB
                        .Select(x => new AuthUserGroup
                        {
                            GroupId = x.AuthGroupId,
                            UserId = UserProfile.Id
                        })
                        .ToListAsync();

                    if (autoMatchGroups.Count > 0)
                    {
                        await db.Set<AuthUserGroup>().AddRangeAsync(autoMatchGroups);
                        await db.SaveChangesAsync();

                        // Optional: refresh UserProfile so DbGroups includes new groups
                        UserProfile = await db.AuthUsers
                            .Include(u => u.AuthUserGroups).ThenInclude(ug => ug.Group)
                            .FirstOrDefaultAsync(u => u.Id == UserProfile.Id);
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
            // Clear previous values just in case this is ever called more than once per service instance
            AdFirstName = null;
            AdLastName = null;
            AdEmail = null;
            AdGroups.Clear();

            if (OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(Username))
            {
                try
                {
                    using var ctx = new PrincipalContext(ContextType.Domain);
                    var samAccountName = Username.GetAfterLastDelimiter("_");

                    using var up = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, samAccountName);
                    if (up != null)
                    {
                        // Basic person info
                        AdFirstName = string.IsNullOrWhiteSpace(up.GivenName) ? null : up.GivenName;
                        AdLastName = string.IsNullOrWhiteSpace(up.Surname) ? null : up.Surname;
                        AdEmail = string.IsNullOrWhiteSpace(up.EmailAddress) ? null : up.EmailAddress;

                        // AD groups
                        try
                        {
                            // Get nested security group memberships
                            var authGroups = up.GetAuthorizationGroups();

                            foreach (var principal in authGroups)
                            {
                                if (principal is GroupPrincipal group)
                                {
                                    var groupName = group.Name;

                                    if (!string.IsNullOrWhiteSpace(groupName))
                                    {
                                        AdGroups.Add(groupName);   // HashSet ensures uniqueness
                                    }
                                }
                            }
                        }
                        catch (PrincipalOperationException)
                        {
                            // Some groups may not be resolvable; ignore group loading failures
                        }

                        return;
                    }
                }
                catch
                {
                    // Ignore AD lookup failures (not domain-joined, permissions, etc.)
                }
            }
        }

        public async Task AutoMatchGroups()
        {
            if (DbGroups.Any()) return;
            return;
        }

        public bool CanAccess(string relativeUrl)
        {
            var path = relativeUrl.NormalizeUrl();

            if (!UserLoaded)
                return false;

            // 1. Get all rules (parent + child)
            var rules = AllRestrictedPages
                .Where(r =>
                    path.StartsWith(r.RelativeUrl, StringComparison.OrdinalIgnoreCase)
                )
                .ToList();

            if (!rules.Any())
                return true; // unrestricted
            var userGroupIds = DbGroups.Select(g => g.Id)
                .ToHashSet();

            bool isSuper = UserProfile.IsSuperuser;

            foreach (var rule in rules)
            {
                // ✔ Superuser short-circuit: if this rule explicitly allows them, skip it entirely.
                if (isSuper && rule.AlwaysAllowSuperusers)
                    continue;
                var pageGroups = rule.RestrictedPageGroups.ToList();
                if (!pageGroups.Any()) return false;
                // 2. Evaluate group membership
                bool userHasGroup =
                    pageGroups.Any(g => userGroupIds.Contains(g.AuthGroupId));

                // 3. If the user (super or not) doesn't meet group requirements → deny
                if (!userHasGroup)
                    return false;
            }

            return true;
        }

    }
}
