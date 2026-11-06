using PolyzenKit.Application.Core;

namespace UserIdentity.Application.Core.Tokens.ViewModels;

public record ExchangeRefreshTokenViewModel : ItemDetailBaseViewModel
{
    public AccessTokenViewModel UserToken { get; init; } = null!;
}
