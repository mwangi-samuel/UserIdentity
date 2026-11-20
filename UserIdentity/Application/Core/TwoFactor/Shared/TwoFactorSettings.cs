using System.ComponentModel.DataAnnotations;

namespace UserIdentity.Application.Core.TwoFactor.Shared;

public sealed class TwoFactorSettings
{
    [Range(1, int.MaxValue)]
    public int TicketLifetimeSeconds { get; init; } = 600;

    [Range(1, int.MaxValue)]
    public int RecoveryCodeCount { get; init; } = 10;
}
