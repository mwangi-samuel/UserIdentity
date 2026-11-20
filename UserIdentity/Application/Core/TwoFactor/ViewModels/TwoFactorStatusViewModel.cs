using PolyzenKit.Application.Core;
using UserIdentity.Common.Enums;

namespace UserIdentity.Application.Core.TwoFactor.ViewModels;

public record TwoFactorStatusViewModel : ItemDetailBaseViewModel
{
    public TwoFactorMethod? Method { get; init; }

    public int RecoveryCodesLeft { get; init; }

    public bool Required { get; init; }

    public required List<TwoFactorMethod> AllowedMethods { get; init; }
}
