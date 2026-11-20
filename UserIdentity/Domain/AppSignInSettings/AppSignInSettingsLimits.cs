using PolyzenKit.Domain.Entities.RegisteredApps;
using UserIdentity.Domain.Roles;

namespace UserIdentity.Domain.AppSignInSettings;

public static class AppSignInSettingsLimits
{
    public const int DefaultRoleMaxLength = RoleLimits.NameMaxLength - RegisteredAppLimits.AppNameMaxLength - 1;
}
