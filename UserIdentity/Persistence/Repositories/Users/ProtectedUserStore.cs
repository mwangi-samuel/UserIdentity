using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using PolyzenKit.Application.Interfaces;
using UserIdentity.Common.Identity;

namespace UserIdentity.Persistence.Repositories.Users;

public class ProtectedUserStore(AppDbContext context, IAppSecretProtector appSecretProtector, IdentityErrorDescriber? describer = null)
    : UserStore<
        IdentityUser,
        IdentityRole,
        AppDbContext,
        string,
        IdentityUserClaim<string>,
        IdentityUserRole<string>,
        IdentityUserLogin<string>,
        IdentityUserToken<string>,
        IdentityRoleClaim<string>,
        IdentityUserPasskey<string>
    >(context, describer)
{
    private const char RecoveryCodeSeparator = ';';

    private readonly IAppSecretProtector _appSecretProtector = appSecretProtector;

    public override Task SetAuthenticatorKeyAsync(IdentityUser user, string key, CancellationToken cancellationToken) =>
        base.SetAuthenticatorKeyAsync(user, _appSecretProtector.Protect(key), cancellationToken);

    public override async Task<string?> GetAuthenticatorKeyAsync(IdentityUser user, CancellationToken cancellationToken) =>
        await base.GetAuthenticatorKeyAsync(user, cancellationToken) is { } storedKey ? _appSecretProtector.Unprotect(storedKey) : null;

    public override Task ReplaceCodesAsync(IdentityUser user, IEnumerable<string> recoveryCodes, CancellationToken cancellationToken)
    {
        var mergedCodes = string.Join(RecoveryCodeSeparator, recoveryCodes);

        return SetTokenAsync(
            user,
            UserTokenNames.InternalLoginProvider,
            UserTokenNames.RecoveryCodes,
            mergedCodes.Length == 0 ? mergedCodes : _appSecretProtector.Protect(mergedCodes),
            cancellationToken
        );
    }

    public override async Task<bool> RedeemCodeAsync(IdentityUser user, string code, CancellationToken cancellationToken)
    {
        var recoveryCodes = await GetRecoveryCodesAsync(user, cancellationToken);

        if (!recoveryCodes.Remove(code))
            return false;

        await ReplaceCodesAsync(user, recoveryCodes, cancellationToken);

        return true;
    }

    public override async Task<int> CountCodesAsync(IdentityUser user, CancellationToken cancellationToken) =>
        (await GetRecoveryCodesAsync(user, cancellationToken)).Count;

    private async Task<List<string>> GetRecoveryCodesAsync(IdentityUser user, CancellationToken cancellationToken)
    {
        var storedCodes = await GetTokenAsync(user, UserTokenNames.InternalLoginProvider, UserTokenNames.RecoveryCodes, cancellationToken);

        return string.IsNullOrEmpty(storedCodes)
            ? []
            : [.. _appSecretProtector.Unprotect(storedCodes).Split(RecoveryCodeSeparator, StringSplitOptions.RemoveEmptyEntries)];
    }
}
