using PolyzenKit.Application.Core;

namespace UserIdentity.Application.Core.AccountLimits.ViewModels;

public record AccountLimitViewModel : BaseViewModel
{
    public bool IsReached { get; init; }
}
