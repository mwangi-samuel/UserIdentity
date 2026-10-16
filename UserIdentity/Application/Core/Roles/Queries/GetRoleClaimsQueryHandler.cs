using Microsoft.AspNetCore.Identity;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Infrastructure.Security.Jwt;

using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.Domain.RoleClaims;

namespace UserIdentity.Application.Core.Roles.Queries;

public record GetRoleClaimsQuery : IBaseQuery
{
  public required string RoleId { get; init; }
}

public record GetRoleClaimsForRolesQuery : IBaseQuery
{
  public required IList<string> Roles { get; init; }
}
public class GetRoleClaimsQueryHandler(
    RoleManager<IdentityRole> roleManager,
    IJwtTokenHandler jwtTokenHandler
    ) : IGetItemsQueryHandler<GetRoleClaimsQuery, RoleClaimsViewModel>,
    IGetItemsQueryHandler<GetRoleClaimsForRolesQuery, RoleClaimsForRolesViewModels>
{
  private readonly RoleManager<IdentityRole> _roleManager = roleManager;
  private readonly IJwtTokenHandler _jwtTokenHandler = jwtTokenHandler;

  public async Task<RoleClaimsViewModel> GetItemsAsync(GetRoleClaimsQuery query, CancellationToken cancellationToken = default)
  {
    var role = await _roleManager.FindByIdAsync(query.RoleId) ?? throw new NoRecordException(query.RoleId, "Role");

    var roleClaims = await _roleManager.GetClaimsAsync(role);

    var rolesCollection = new List<RoleClaimDTO>();

    foreach (var roleClaim in roleClaims)
    {
      (var resource, var action) = _jwtTokenHandler.DecodeScopeClaim(roleClaim);

      RoleClaimDTO claimDTO = new()
      {
        Resource = resource,
        Action = action,
        Scope = roleClaim.Value
      };

      rolesCollection.Add(claimDTO);
    }

    return new RoleClaimsViewModel
    {
      RoleClaims = rolesCollection
    };

  }

  public async Task<RoleClaimsForRolesViewModels> GetItemsAsync(GetRoleClaimsForRolesQuery getRoleClaimsForRolesQuery, CancellationToken cancellationToken = default)
  {
    HashSet<string> roleClaims = [];

    foreach (var role in getRoleClaimsForRolesQuery.Roles)
    {

      var currentRole = await _roleManager.FindByNameAsync(role);

      if (currentRole != null)
      {
        var currentRoleClaims = await _roleManager.GetClaimsAsync(currentRole);

        foreach (var claim in currentRoleClaims)
        {
          roleClaims.Add(claim.Value);
        }
      }
    }

    return new RoleClaimsForRolesViewModels
    {
      RoleClaims = roleClaims
    };
  }
}
