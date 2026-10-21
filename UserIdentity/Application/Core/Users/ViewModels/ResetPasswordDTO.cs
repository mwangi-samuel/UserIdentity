namespace UserIdentity.Application.Core.Users.ViewModels;

public record ResetPasswordDTO
{
  public string EmailMessage { get; init; } = null!;

  public string ResetRequestId { get; init; } = null!;
}
