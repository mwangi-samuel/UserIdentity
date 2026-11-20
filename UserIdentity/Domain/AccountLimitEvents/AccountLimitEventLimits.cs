namespace UserIdentity.Domain.AccountLimitEvents;

public static class AccountLimitEventLimits
{
    public const int KindMaxLength = 30;

    public static readonly TimeSpan CountWindow = TimeSpan.FromDays(1);
}
