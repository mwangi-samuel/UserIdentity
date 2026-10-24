using PolyzenKit.Application.Core;

using UserIdentity.Domain.Roles;

namespace UserIdentity.Application.Core.Roles.ViewModels;

public record RolesViewModel : ItemsBaseViewModel
{
  public ICollection<RoleDto> Roles { get; init; } = null!;
}

public record UserRolesViewModel : ItemsBaseViewModel
{
  public ICollection<string> UserRoles { get; init; } = null!;
}
