namespace UserIdentity.Application.Core.Users.ViewModels;

public record ResetPasswordDto
{
  public string EmailMessage { get; init; } = null!;

  public string ResetRequestId { get; init; } = null!;
}
