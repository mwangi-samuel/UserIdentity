using Microsoft.AspNetCore.Identity;
using PolyzenKit.Common.Exceptions;

namespace UserIdentity.Application.Core.Users.Shared;

public static class IdentityResults
{
    public static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new RecordUpdateException(string.Join(" ", result.Errors.Select(error => error.Description)));
    }
}
