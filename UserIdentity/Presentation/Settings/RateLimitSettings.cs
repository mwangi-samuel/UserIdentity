namespace UserIdentity.Presentation.Settings;

public record RateLimitSettings
{
  public const string ClientIpPolicy = "ClientIpPolicy";

  public bool Enabled { get; set; }
  public int PermitLimit { get; set; }
  public int WindowSeconds { get; set; }
}
