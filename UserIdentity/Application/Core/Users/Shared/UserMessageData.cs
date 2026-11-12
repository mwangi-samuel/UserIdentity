namespace UserIdentity.Application.Core.Users.Shared;

public sealed record UserMessageData
{
    public string? UserId { get; init; }

    public string? UserName { get; init; }

    public string? FirstName { get; init; }

    public string? LastName { get; init; }
}
