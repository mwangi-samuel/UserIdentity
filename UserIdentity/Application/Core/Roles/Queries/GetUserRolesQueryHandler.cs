using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Presentation.Settings;

using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.Application.Helpers;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;

namespace UserIdentity.Application.Core.Roles.Queries;

public record GetUserRolesQuery : IBaseQuery
{
  public required string UserId { get; init; }

  public string? AppName { get; init; }
}

public class GetUserRolesQueryHandler(
    RoleManager<IdentityRole> roleManager,
    UserManager<IdentityUser> userManager,
    IUserRegisteredAppRepository userRegisteredAppRepository,
    IOptions<RoleSettings> roleSettingsOptions
    ) : IGetItemsQueryHandler<GetUserRolesQuery, UserRolesViewModel>
{
  private readonly RoleManager<IdentityRole> _roleManager = roleManager;
  private readonly UserManager<IdentityUser> _userManager = userManager;
  private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = userRegisteredAppRepository;
  private readonly RoleSettings _roleSettings = roleSettingsOptions.Value;

  public async Task<UserRolesViewModel> GetItemsAsync(GetUserRolesQuery query, CancellationToken cancellationToken = default)
  {

    var user = await _userManager.FindByIdAsync(query.UserId) ?? throw new NoRecordException(query.UserId, "User");

    if (query.AppName is not null && !await _userRegisteredAppRepository.IsUserRegisteredForAppAsync(query.UserId, query.AppName, cancellationToken))
      throw new NoRecordException(query.UserId, "User");

    var userRoles = await _userManager.GetRolesAsync(user) ?? [];

    if (query.AppName is not null)
      userRoles = AppRolesHelper.ForApp(userRoles, query.AppName, _roleSettings.GlobalAdminRole);

    return new UserRolesViewModel
    {
      UserRoles = userRoles
    };
  }
}
