namespace UserIdentity.Infrastructure.ExternalServices;

public record GoogleRecaptchaResponseDTO
{
  public bool Success { get; set; }
  public float Score { get; set; }
  public string? Action { get; set; }
  public string? ChallengeTs { get; set; }
  public string? Hostname { get; set; }
  public string[]? ErrorCodes { get; set; }
}
