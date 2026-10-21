namespace UserIdentity.Application.Core.Tokens.ViewModels;

public record AccessTokenDTO
{
  public string? Token { get; init; }

  public int ExpiresIn { get; init; }
}
