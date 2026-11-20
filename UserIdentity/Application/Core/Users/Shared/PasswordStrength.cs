using Microsoft.AspNetCore.Identity;

namespace UserIdentity.Application.Core.Users.Shared;

public static class PasswordStrength
{
    public static async Task EnsureValidAsync(UserManager<IdentityUser> userManager, IdentityUser user, string password)
    {
        List<string> errors = [];

        foreach (var validator in userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(userManager, user, password);

            errors.AddRange(result.Errors.Select(error => error.Description));
        }

        if (errors.Count > 0)
            throw new InvalidDataException(string.Join(" ", errors));
    }
}
