using UserIdentity.Common.Enums;

namespace UserIdentity.Application.Core.Users.ViewModels;

public record UserListItemDto
{
    public required string Id { get; init; }

    public string? UserName { get; init; }

    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    public string? Email { get; init; }

    public required bool EmailConfirmed { get; init; }

    public string? PhoneNumber { get; init; }

    public TwoFactorMethod? TwoFactorMethod { get; init; }

    public DateTime? CreatedAt { get; init; }

    public required List<string> Roles { get; init; }

    public DateTime? LastActiveAt { get; init; }

    public required bool LockedOut { get; init; }

    public required bool DeleteEnabled { get; init; }
}
