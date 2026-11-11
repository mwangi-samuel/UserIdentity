using PolyzenKit.Application.Core;

namespace UserIdentity.Application.Core.Tokens.ViewModels;

public record UserTokenViewModel : BaseViewModel
{
    public required UserTokenDto UserToken { get; init; }
}
