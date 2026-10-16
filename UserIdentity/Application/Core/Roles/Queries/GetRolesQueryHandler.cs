using Microsoft.AspNetCore.Identity;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;

using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.Domain.Roles;

namespace UserIdentity.Application.Core.Roles.Queries;

public record GetRolesQuery : IBaseQuery
{
}

public class GetRolesQueryHandler(
    RoleManager<IdentityRole> roleManager
    ) : IGetItemsQueryHandler<GetRolesQuery, RolesViewModel>
{
  private readonly RoleManager<IdentityRole> _roleManager = roleManager;

  public async Task<RolesViewModel> GetItemsAsync(GetRolesQuery query, CancellationToken cancellationToken = default)
  {
    var roles = _roleManager
    .Roles
    .Select(r => new RoleDTO { Id = r.Id, Name = r.Name! })
    .ToList();

    return new RolesViewModel
    {
      Roles = roles
    };
  }
}
