using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;

namespace UserIdentity.Application.Core.Users.Shared;

public static class CodeOutcomes
{
    public static void EnsureVerified(CodeCheckOutcome outcome)
    {
        if (outcome != CodeCheckOutcome.Verified)
            throw new InvalidDataException(ErrorMessages.Codes.NotAccepted);
    }
}
