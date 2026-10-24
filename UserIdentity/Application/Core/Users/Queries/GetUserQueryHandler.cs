using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Enums;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Domain.Dto;
using PolyzenKit.Presentation.Settings;

using UserIdentity.Application.Core.Roles.Queries;
using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Application.Helpers;
using UserIdentity.Common;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.Users.Queries;

public record GetUserQuery : IBaseQuery
{
  public required string UserId { get; init; }

  public string? AppName { get; init; }
}

public class GetUserQueryHandler(
    UserManager<IdentityUser> userManager,
    IUserRepository userRepository,
    IUserRegisteredAppRepository userRegisteredAppRepository,
    IOptions<RoleSettings> roleSettingsOptions,
    IGetItemsQueryHandler<GetRoleClaimsForRolesQuery, RoleClaimsForRolesViewModels> getRoleClaimsQueryHandler
    ) : IGetItemQueryHandler<GetUserQuery, UserViewModel>
{

  private readonly UserManager<IdentityUser> _userManager = userManager;
  private readonly IUserRepository _userRepository = userRepository;
  private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = userRegisteredAppRepository;
  private readonly RoleSettings _roleSettings = roleSettingsOptions.Value;
  private readonly IGetItemsQueryHandler<GetRoleClaimsForRolesQuery, RoleClaimsForRolesViewModels> _getRoleClaimsQueryHandler = getRoleClaimsQueryHandler;

  public async Task<UserViewModel> GetItemAsync(GetUserQuery query, CancellationToken cancellationToken = default)
  {
    var user = await _userManager.FindByIdAsync(query.UserId) ?? throw new NoRecordException($"{query.UserId}", EntityTypes.USER.Description());

    if (query.AppName is not null && !await _userRegisteredAppRepository.IsUserRegisteredForAppAsync(query.UserId, query.AppName, cancellationToken))
      throw new NoRecordException($"{query.UserId}", EntityTypes.USER.Description());

    var userDetails = await _userRepository.GetEntityItemAsync(query.UserId, cancellationToken);

    var userRoles = await _userManager.GetRolesAsync(user);

    if (query.AppName is not null)
      userRoles = AppRolesHelper.ForApp(userRoles, query.AppName, _roleSettings.GlobalAdminRole);

    var userRoleClaims = await _getRoleClaimsQueryHandler.GetItemsAsync(new GetRoleClaimsForRolesQuery { Roles = userRoles }, cancellationToken);

    var userDto = new UserDto
    {
      Id = user.Id,
      UserName = user.UserName,
      FirstName = userDetails.FirstName,
      LastName = userDetails.LastName,
      Email = user.Email,
      PhoneNumber = user.PhoneNumber,
      Roles = [.. userRoles],
      RoleClaims = userRoleClaims.RoleClaims
    };

    userDto.SetDtoAuditFields(userDetails);

    return new UserViewModel
    {
      User = userDto
    };
  }
}
