using Microsoft.AspNetCore.Identity;

namespace UserIdentity.Application.Core.Users.Shared;

public static class AccountConfirmation
{
    public static bool IsConfirmed(IdentityOptions identityOptions, IdentityUser user) =>
        !identityOptions.SignIn.RequireConfirmedAccount || user.EmailConfirmed || user.PhoneNumberConfirmed;
}
