using PolyzenKit.Application.Core;

using UserIdentity.Domain.Users;

namespace UserIdentity.Application.Core.Users.ViewModels;

public record UserViewModel : ItemDetailBaseViewModel
{
  public required UserDto User { get; init; }
}
