using PolyzenKit.Application.Core;

namespace UserIdentity.Application.Core.Roles.ViewModels;

public record UserRolesViewModel : ItemsBaseViewModel
{
    public required List<string> UserRoles { get; init; }
}
