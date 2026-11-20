using Microsoft.AspNetCore.Identity;

namespace UserIdentity.Application.Core.TwoFactor.Shared;

public static class AuthenticatorCodes
{
    public static Task<bool> IsValidAsync(UserManager<IdentityUser> userManager, IdentityUser user, string code) =>
        userManager.VerifyTwoFactorTokenAsync(
            user,
            userManager.Options.Tokens.AuthenticatorTokenProvider,
            code.Replace(" ", string.Empty).Replace("-", string.Empty)
        );

    public static string KeyUri(string issuer, string accountName, string key) =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(accountName)}?secret={key}&issuer={Uri.EscapeDataString(issuer)}&digits=6";
}
