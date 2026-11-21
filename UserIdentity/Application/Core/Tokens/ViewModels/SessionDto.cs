namespace UserIdentity.Application.Core.Tokens.ViewModels;

public record SessionDto
{
    public required Guid Id { get; init; }

    public DateTime? SignedInAt { get; init; }

    public DateTime? LastActiveAt { get; init; }

    public required DateTime ExpiresAt { get; init; }

    public string? IpAddress { get; init; }

    public string? Device { get; init; }

    public required bool Current { get; init; }
}
