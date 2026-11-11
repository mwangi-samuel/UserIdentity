using PolyzenKit.Application.Core;
using UserIdentity.Domain.Roles;

namespace UserIdentity.Application.Core.Roles.ViewModels;

public record RolesViewModel : ItemsBaseViewModel
{
    public required List<RoleDto> Roles { get; init; }
}
