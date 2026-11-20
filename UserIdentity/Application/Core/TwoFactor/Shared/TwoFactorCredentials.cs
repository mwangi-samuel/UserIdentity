using Microsoft.AspNetCore.Identity;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Common.Identity;

namespace UserIdentity.Application.Core.TwoFactor.Shared;

public static class TwoFactorCredentials
{
    public static async Task RemoveAuthenticatorKeyAsync(UserManager<IdentityUser> userManager, IdentityUser user) =>
        IdentityResults.EnsureSucceeded(
            await userManager.RemoveAuthenticationTokenAsync(user, UserTokenNames.InternalLoginProvider, UserTokenNames.AuthenticatorKey)
        );

    public static async Task RemoveAsync(UserManager<IdentityUser> userManager, IdentityUser user)
    {
        await RemoveAuthenticatorKeyAsync(userManager, user);

        IdentityResults.EnsureSucceeded(
            await userManager.RemoveAuthenticationTokenAsync(user, UserTokenNames.InternalLoginProvider, UserTokenNames.RecoveryCodes)
        );
        IdentityResults.EnsureSucceeded(await userManager.SetTwoFactorEnabledAsync(user, false));
    }
}
