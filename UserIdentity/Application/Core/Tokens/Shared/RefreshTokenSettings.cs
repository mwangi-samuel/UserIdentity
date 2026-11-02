namespace UserIdentity.Application.Core.Tokens.Shared;

public record RefreshTokenSettings
{
  public int ValidForSeconds { get; set; }
}
