using PolyzenKit.Application.Core;

namespace UserIdentity.Application.Core.Users.ViewModels;

public record UpdatePasswordViewModel : BaseViewModel
{
    public required UpdatePasswordDto UpdatePasswordResult { get; init; }
}
