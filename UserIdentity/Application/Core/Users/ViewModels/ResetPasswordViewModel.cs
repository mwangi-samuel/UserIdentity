using PolyzenKit.Application.Core;

namespace UserIdentity.Application.Core.Users.ViewModels;

public record ResetPasswordViewModel : BaseViewModel
{
    public required ResetPasswordDto ResetPasswordDetails { get; init; }
}
