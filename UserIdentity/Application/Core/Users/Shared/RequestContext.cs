namespace UserIdentity.Application.Core.Users.Shared;

public sealed record RequestContext
{
    public const int DeviceMaxLength = 256;

    public string? IpAddress { get; init; }

    public string? Device { get; init; }

    public static RequestContext Create(string? ipAddress, string? userAgent) =>
        new()
        {
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress,
            Device = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent[..Math.Min(userAgent.Length, DeviceMaxLength)],
        };
}
