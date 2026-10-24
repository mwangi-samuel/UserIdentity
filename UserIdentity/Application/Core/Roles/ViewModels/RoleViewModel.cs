using PolyzenKit.Application.Core;

using UserIdentity.Domain.Roles;

namespace UserIdentity.Application.Core.Roles.ViewModels;

public record RoleViewModel : ItemDetailBaseViewModel
{
  public RoleDto Role { get; init; } = null!;
}
