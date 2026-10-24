using PolyzenKit.Application.Core;

using UserIdentity.Domain.RoleClaims;

namespace UserIdentity.Application.Core.Roles.ViewModels;

public record RoleClaimViewModel : ItemDetailBaseViewModel
{
  public RoleClaimDto RoleClaim { get; init; } = null!;
}
