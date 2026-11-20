using System.ComponentModel.DataAnnotations;

namespace UserIdentity.Application.Core.AccountLimits.Shared;

public sealed class AccountLimitSettings
{
    [Range(1, int.MaxValue)]
    public int MaxFailedSecondStepsPerDay { get; init; } = 10;

    [Range(1, int.MaxValue)]
    public int MaxLoginCodesPerDay { get; init; } = 10;

    [Range(0, int.MaxValue)]
    public int SignUpAttemptNoticeCooldownSeconds { get; init; } = 60;

    [Range(1, int.MaxValue)]
    public int MaxSignUpAttemptNoticesPerDay { get; init; } = 5;
}
