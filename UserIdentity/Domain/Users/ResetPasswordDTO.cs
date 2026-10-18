namespace UserIdentity.Domain.Users;

public record ResetPasswordDTO
{
  public string EmailMessage { get; init; } = null!;

  public string ResetRequestId { get; init; } = null!;
}
