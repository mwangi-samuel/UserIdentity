using UserIdentity.Domain.Users;

namespace UserIdentity.Domain.EmailChanges;

public static class EmailChangeLimits
{
    public const int NewEmailMaxLength = UserLimits.EmailMaxLength;
}
