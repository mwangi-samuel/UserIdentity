using PolyzenKit.Application.Core;

namespace UserIdentity.Application.Core.TwoFactor.ViewModels;

public record RecoveryCodesViewModel : BaseViewModel
{
    public required List<string> RecoveryCodes { get; init; }
}
