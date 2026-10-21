using PolyzenKit.Application.Core;

namespace UserIdentity.Application.Core.Users.ViewModels;

public record UpdatePasswordViewModel : BaseViewModel
{
  public UpdatePasswordDTO UpdatePasswordResult { get; init; } = null!;
}

