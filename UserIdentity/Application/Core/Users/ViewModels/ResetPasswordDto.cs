namespace UserIdentity.Application.Core.Users.ViewModels;

public record ResetPasswordDto
{
    public required string EmailMessage { get; init; }

    public required string ResetRequestId { get; init; }
}
