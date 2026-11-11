using Microsoft.AspNetCore.Identity;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Extensions;
using PolyzenKit.Common.Settings;
using PolyzenKit.Common.Utilities;

namespace UserIdentity.Application.Core.Roles.Shared;

public static class AppRoleNames
{
    // Always admin roles, since token consumers may treat <app>:admin as an admin role
    // whatever this service's AdminRoles says
    private static readonly HashSet<string> _defaultAdminRoles = RoleSettings.ParseRoles(KnownRoles.Admin);

    public static string Prefix(string appName) => $"{appName.TrimAndLowerInvariant()}{Separators.ServiceRole}";

    public static string DefaultRole(string appName, RoleSettings roleSettings) =>
        $"{Prefix(appName)}{roleSettings.DefaultRole.TrimAndLowerInvariant()}";

    public static bool IsAppRole(string? roleName, string appName) =>
        roleName is not null && roleName.StartsWith(Prefix(appName), StringComparison.OrdinalIgnoreCase);

    public static async Task<IdentityRole> GetAppRoleAsync(RoleManager<IdentityRole> roleManager, string roleId, string? appName)
    {
        var role = await roleManager.FindByIdAsync(roleId);

        if (role is null || (appName is not null && !IsAppRole(role.Name, appName)))
            throw NoRecordException.ForIdentifier(roleId);

        return role;
    }

    public static bool IsAppAdminRole(string? roleName, string appName, RoleSettings roleSettings)
    {
        var trimmedRoleName = roleName?.Trim();

        if (!IsAppRole(trimmedRoleName, appName))
            return false;

        var role = trimmedRoleName![Prefix(appName).Length..].ToLowerInvariant();

        return _defaultAdminRoles.Contains(role) || roleSettings.AdminRoleSet.Contains(role);
    }

    public static bool IsGlobalAdminRole(string? roleName, string? globalAdminRole) =>
        !string.IsNullOrWhiteSpace(globalAdminRole) && string.Equals(roleName, globalAdminRole.Trim(), StringComparison.OrdinalIgnoreCase);

    // Tokens and app admins only see the app's own roles, plus the global admin role, which isn't tied to an app
    public static List<string> ForApp(IEnumerable<string> roles, string appName, string? globalAdminRole) =>
        [.. roles.Where(role => IsAppRole(role, appName) || IsGlobalAdminRole(role, globalAdminRole))];
}
