using UserIdentity.Domain.RefreshTokens;

namespace UserIdentity.Application.Core.Users.Shared;

public sealed record RequestContext
{
    public string? IpAddress { get; init; }

    public string? Device { get; init; }

    public static RequestContext Create(string? ipAddress, string? userAgent) =>
        new()
        {
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress,
            Device = string.IsNullOrWhiteSpace(userAgent)
                ? null
                : userAgent[..Math.Min(userAgent.Length, RefreshTokenLimits.DeviceMaxLength)],
        };
}
