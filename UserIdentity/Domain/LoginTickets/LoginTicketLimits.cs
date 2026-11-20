using UserIdentity.Domain.Users;

namespace UserIdentity.Domain.LoginTickets;

public static class LoginTicketLimits
{
    public const int TicketMaxLength = 200;
    public const int StepMaxLength = 20;
    public const int CodeActionMaxLength = 50;
    public const int CodeCorrelationIdMaxLength = 100;
    public const int MethodMaxLength = 20;
    public const int PhoneNumberMaxLength = UserLimits.PhoneNumberMaxLength;
}
