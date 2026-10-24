namespace UserIdentity.Application.Core.Tokens.ViewModels;

public record AccessTokenDto
{
  public string? Token { get; init; }

  public int ExpiresIn { get; init; }
}
