using PolyzenKit.Application.Core;
using UserIdentity.Common.Enums;

namespace UserIdentity.Application.Core.TwoFactor.ViewModels;

public record TwoFactorSetupViewModel : BaseViewModel
{
    public required TwoFactorMethod Method { get; init; }

    public string? AuthenticatorKey { get; init; }

    public string? AuthenticatorUri { get; init; }

    public string? CodeSentTo { get; init; }
}
