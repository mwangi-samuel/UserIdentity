namespace UserIdentity.Infrastructure.ExternalServices;

public record GoogleRecaptchaSettings
{
  public bool Enabled { get; set; }
  public string? SiteKey { get; set; }
}
