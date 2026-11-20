using Microsoft.AspNetCore.Identity;

namespace UserIdentity.Application.Core.Users.Shared;

public static class UserEmailUpdate
{
    public static async Task ApplyAsync(UserManager<IdentityUser> userManager, IdentityUser user, string newEmail, bool confirmed)
    {
        var userNameFollowsEmail = string.Equals(user.UserName, user.Email, StringComparison.OrdinalIgnoreCase);

        IdentityResults.EnsureSucceeded(await userManager.SetEmailAsync(user, newEmail));

        if (confirmed)
        {
            user.EmailConfirmed = true;
            IdentityResults.EnsureSucceeded(await userManager.UpdateAsync(user));
        }

        if (userNameFollowsEmail)
            IdentityResults.EnsureSucceeded(await userManager.SetUserNameAsync(user, newEmail));
    }
}
