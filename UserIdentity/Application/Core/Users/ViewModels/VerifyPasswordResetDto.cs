namespace UserIdentity.Application.Core.Users.ViewModels;

public record VerifyPasswordResetDto
{
  public bool PasswordResetVerified { get; init; }
}
