using PolyzenKit.Application.Core;

using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Domain.Users;

namespace UserIdentity.Application.Core.Users.ViewModels;

public record AuthUserViewModel : ItemDetailBaseViewModel
{
  public UserDto User { get; init; } = null!;
  public bool IsConfirmed { get; init; }
  public AccessTokenViewModel? UserToken { get; init; }
}
