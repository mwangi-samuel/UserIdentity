namespace UserIdentity.Domain.PasswordResets;

public static class PasswordResetLimits
{
    public const int RequestIdMaxLength = 200;

    // Expired requests are kept this long, so MaxRequestsPerDay can count them
    public static readonly TimeSpan RequestCountWindow = TimeSpan.FromDays(1);
}
