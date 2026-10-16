using Microsoft.AspNetCore.Identity;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;

using UserIdentity.Application.Core.Roles.ViewModels;

namespace UserIdentity.Application.Core.Roles.Queries;

public record GetUserRolesQuery : IBaseQuery
{
  public required string UserId { get; init; }
}

public class GetUserRolesQueryHandler(
    RoleManager<IdentityRole> roleManager,
    UserManager<IdentityUser> userManager
    ) : IGetItemsQueryHandler<GetUserRolesQuery, UserRolesViewModel>
{
  private readonly RoleManager<IdentityRole> _roleManager = roleManager;
  private readonly UserManager<IdentityUser> _userManager = userManager;

  public async Task<UserRolesViewModel> GetItemsAsync(GetUserRolesQuery query, CancellationToken cancellationToken = default)
  {

    var user = await _userManager.FindByIdAsync(query.UserId) ?? throw new NoRecordException(query.UserId, "User");

    var userRoles = await _userManager.GetRolesAsync(user) ?? [];

    return new UserRolesViewModel
    {
      UserRoles = userRoles
    };
  }
}
