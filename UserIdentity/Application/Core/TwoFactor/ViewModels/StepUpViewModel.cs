using PolyzenKit.Application.Core;

namespace UserIdentity.Application.Core.TwoFactor.ViewModels;

public record StepUpViewModel : BaseViewModel
{
    public required string Ticket { get; init; }

    public required DateTime ExpiresAt { get; init; }
}
