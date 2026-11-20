using Microsoft.AspNetCore.Identity;
using UserIdentity.Domain.AppSignInSettings;

namespace UserIdentity.Application.Core.Users.Shared;

public static class AccountConfirmation
{
    public static bool IsConfirmed(AppSignInSettingsDto signInSettings, IdentityUser user) =>
        !signInSettings.RequireConfirmedAccount || HasConfirmedContact(user);

    public static bool HasConfirmedContact(IdentityUser user) => user.EmailConfirmed || user.PhoneNumberConfirmed;
}
