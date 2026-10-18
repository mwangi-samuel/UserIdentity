namespace UserIdentity.Application.Core.Tokens.Settings;

public record RefreshTokenSettings
{
  public int ValidForSeconds { get; set; }
}
