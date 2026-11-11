using UserIdentity.Domain.Roles;
using UserIdentity.Domain.Users;

namespace UserIdentity.Domain.InviteCodes;

public static class InviteCodeLimits
{
    public const int InviteCodeMinLength = 10;
    public const int InviteCodeMaxLength = 255;
    public const int UserEmailMaxLength = UserLimits.EmailMaxLength;
    public const int RoleMaxLength = RoleLimits.NameMaxLength;
    public const int SearchMaxLength = 100;
}
