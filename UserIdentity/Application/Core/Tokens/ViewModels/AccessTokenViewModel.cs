using PolyzenKit.Application.Core;

namespace UserIdentity.Application.Core.Tokens.ViewModels;

public record AccessTokenViewModel : BaseViewModel
{
  public AccessTokenDto? AccessToken { get; init; }

  public string? RefreshToken { get; init; }
}
