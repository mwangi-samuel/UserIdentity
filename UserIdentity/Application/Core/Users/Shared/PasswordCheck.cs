using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core.Interfaces;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Common.Errors;

namespace UserIdentity.Application.Core.Users.Shared;

public static class PasswordCheck
{
    public static async Task EnsureAsync(
        UserManager<IdentityUser> userManager,
        ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel> recordFailedSignInCommandHandler,
        IdentityUser user,
        string password,
        CancellationToken cancellationToken
    )
    {
        if (await userManager.IsLockedOutAsync(user))
            throw new InvalidDataException(ErrorMessages.Users.AccountLocked);

        if (await userManager.CheckPasswordAsync(user, password))
            return;

        await recordFailedSignInCommandHandler.CreateItemAsync(new RecordFailedSignInCommand(user, false), user.Id, cancellationToken);

        throw new InvalidDataException(ErrorMessages.Users.WrongPassword);
    }
}
