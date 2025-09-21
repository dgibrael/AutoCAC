using AutoCAC.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.DirectoryServices.AccountManagement;

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

        public UserContextService(
            IDbContextFactory<mainContext> dbFactory,
            AuthenticationStateProvider authProvider)
        {
            _dbFactory = dbFactory;
            _authProvider = authProvider;
        }

        public async Task InitializeAsync()
        {
            if (UserProfile != null)
                return; // avoid double-initialization

            var authState = await _authProvider.GetAuthenticationStateAsync();
            CurrentUser = authState.User;

            if (!CurrentUser.Identity?.IsAuthenticated ?? false)
                return;

            await using var db = await _dbFactory.CreateDbContextAsync();
            UserProfile = await db.AuthUsers
                .Include(u => u.AuthUserGroups)
                    .ThenInclude(ug => ug.Group)
                .FirstOrDefaultAsync(u => u.Username.ToLower() == Username);
        }
    }

}
