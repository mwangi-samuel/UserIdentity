using Microsoft.AspNetCore.Identity;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Settings;
using UserIdentity.Application.Core.Roles.Shared;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;

namespace UserIdentity.Application.Core.Users.Shared;

public static class UserChangeAccess
{
    public static async Task<IdentityUser> EnsureAllowedAsync(
        UserManager<IdentityUser> userManager,
        IUserRegisteredAppRepository userRegisteredAppRepository,
        RoleSettings roleSettings,
        IdentityUser? user,
        string identifier,
        string appName,
        UserChanger changer,
        CancellationToken cancellationToken
    )
    {
        if (
            user is null
            || !await userRegisteredAppRepository.IsUserRegisteredForAppAsync(user.Id, appName, cancellationToken)
            || !IsAllowed(changer, user.Id, await userManager.GetRolesAsync(user), appName, roleSettings)
        )
            throw NoRecordException.ForIdentifier(identifier);

        return user;
    }

    public static bool IsAllowed(
        UserChanger changer,
        string userId,
        IEnumerable<string> roles,
        string appName,
        RoleSettings roleSettings
    ) => changer.IsGlobalAdmin || changer.IsUser(userId) || !AppRoleNames.HoldsAdminRole(roles, appName, roleSettings);
}
