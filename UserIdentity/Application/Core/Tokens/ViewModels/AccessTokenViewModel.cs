using PolyzenKit.Application.Core;

namespace UserIdentity.Application.Core.Tokens.ViewModels;

public record AccessTokenViewModel : BaseViewModel
{
    public required AccessTokenDto AccessToken { get; init; }
}
