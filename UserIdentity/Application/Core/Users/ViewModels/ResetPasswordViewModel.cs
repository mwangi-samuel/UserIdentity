using PolyzenKit.Application.Core;

namespace UserIdentity.Application.Core.Users.ViewModels;

public record ResetPasswordViewModel : BaseViewModel
{
  public ResetPasswordDto ResetPasswordDetails { get; init; } = null!;
}
