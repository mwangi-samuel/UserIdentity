using PolyzenKit.Common.Utilities;
using PolyzenKit.Presentation.Helpers;

namespace UserIdentity.Application.Helpers;

public static class AppRolesHelper
{
  public static bool IsAppRole(string? roleName, string appName) =>
    roleName is not null && roleName.StartsWith($"{appName.TrimAndLowerInvariant()}{ZenConstants.SERVICE_ROLE_SEPARATOR}", StringComparison.OrdinalIgnoreCase);

  // Always includes <app>:admin, since token consumers may treat it as an admin role whatever this service's AdminRoles says
  public static bool IsAppAdminRole(string? roleName, string appName, string? adminRoles) =>
    roleName is not null && RequestUserHelper.ParseAdminRoles($"{ZenConstants.DEFAULT_ADMIN_ROLES}{ZenConstants.LIST_CONFIG_SEPARATOR}{adminRoles}")
      .Any(adminRole => string.Equals(roleName.Trim(), $"{appName.TrimAndLowerInvariant()}{ZenConstants.SERVICE_ROLE_SEPARATOR}{adminRole}", StringComparison.OrdinalIgnoreCase));

  public static bool IsGlobalAdminRole(string? roleName, string? globalAdminRole) =>
    !string.IsNullOrWhiteSpace(globalAdminRole) && string.Equals(roleName, globalAdminRole.Trim(), StringComparison.OrdinalIgnoreCase);

  // Tokens and app admins only see the app's own roles, plus the global admin role, which isn't tied to an app
  public static List<string> ForApp(IEnumerable<string> roles, string appName, string? globalAdminRole) =>
    [.. roles.Where(role => IsAppRole(role, appName) || IsGlobalAdminRole(role, globalAdminRole))];
}
