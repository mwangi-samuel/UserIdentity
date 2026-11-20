using UserIdentity.Common.Enums;

namespace UserIdentity.Application.Core.Users.Shared;

public sealed record UserMessageData
{
    public string? FirstName { get; init; }

    public LoginCodePurpose? Purpose { get; init; }

    public string? Change { get; init; }

    public TwoFactorMethod? Method { get; init; }

    public DateTime? RequestedAt { get; init; }

    public DateTime? AttemptedAt { get; init; }

    public DateTime? ChangedAt { get; init; }

    public string? IpAddress { get; init; }

    public string? Device { get; init; }
}
