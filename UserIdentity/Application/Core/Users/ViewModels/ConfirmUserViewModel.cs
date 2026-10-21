using PolyzenKit.Application.Core;

namespace UserIdentity.Application.Core.Users.ViewModels;

public record ConfirmUserViewModel : ItemDetailBaseViewModel
{
  public required ConfirmUserDTO ConfirmUserResult { get; init; }
}
