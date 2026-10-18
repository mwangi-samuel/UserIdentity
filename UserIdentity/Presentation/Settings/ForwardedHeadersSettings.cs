namespace UserIdentity.Presentation.Settings;

public record ForwardedHeadersSettings
{
  // Comma separated CIDR ranges, e.g. "10.42.0.0/16"
  public string? KnownNetworks { get; set; }
}
