using PolyzenKit.Application.Core;

namespace UserIdentity.Application.Core.Users.ViewModels;

public record BootstrappedAdminViewModel : BaseViewModel
{
    public string? UserId { get; init; }
}
