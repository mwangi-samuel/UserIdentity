using Microsoft.AspNetCore.Identity;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;

using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.Application.Helpers;
using UserIdentity.Domain.Roles;

namespace UserIdentity.Application.Core.Roles.Queries;

public record GetRolesQuery : IBaseQuery
{
  public string? AppName { get; init; }
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
    .Select(r => new RoleDto { Id = r.Id, Name = r.Name! })
    .AsEnumerable()
    .Where(r => query.AppName is null || AppRolesHelper.IsAppRole(r.Name, query.AppName))
    .ToList();

    return new RolesViewModel
    {
      Roles = roles
    };
  }
}
