using PolyzenKit.Application.Core;
using UserIdentity.Domain.RoleClaims;

namespace UserIdentity.Application.Core.Roles.ViewModels;

public record RoleClaimsViewModel : ItemsBaseViewModel
{
    public required List<RoleClaimDto> RoleClaims { get; init; }
}
