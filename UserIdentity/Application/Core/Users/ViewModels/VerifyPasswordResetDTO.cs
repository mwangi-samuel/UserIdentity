namespace UserIdentity.Application.Core.Users.ViewModels;

public record VerifyPasswordResetDTO
{
  public bool PasswordResetVerified { get; init; }
}
