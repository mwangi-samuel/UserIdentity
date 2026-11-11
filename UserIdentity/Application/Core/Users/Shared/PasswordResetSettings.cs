using System.ComponentModel.DataAnnotations;

namespace UserIdentity.Application.Core.Users.Shared;

public sealed class PasswordResetSettings
{
    [Range(1, int.MaxValue)]
    public required int ValidForSeconds { get; init; }

    [Range(0, int.MaxValue)]
    public int RequestCooldownSeconds { get; init; }

    [Range(1, int.MaxValue)]
    public int MaxRequestsPerDay { get; init; } = 5;
}
