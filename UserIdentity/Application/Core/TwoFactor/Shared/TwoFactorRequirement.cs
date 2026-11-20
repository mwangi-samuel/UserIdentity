using PolyzenKit.Common.Settings;
using UserIdentity.Application.Core.Roles.Shared;
using UserIdentity.Domain.AppSignInSettings;

namespace UserIdentity.Application.Core.TwoFactor.Shared;

public static class TwoFactorRequirement
{
    public static bool IsRequired(
        AppSignInSettingsDto signInSettings,
        IEnumerable<string> roles,
        string appName,
        RoleSettings roleSettings
    ) =>
        signInSettings.RequireTwoFactor
        || (signInSettings.RequireTwoFactorForAdmins && AppRoleNames.HoldsAdminRole(roles, appName, roleSettings));
}
