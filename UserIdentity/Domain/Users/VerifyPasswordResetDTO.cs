namespace UserIdentity.Domain.Users;

public record VerifyPasswordResetDTO
{
  public bool PasswordResetVerified { get; init; }
}
