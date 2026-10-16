using Microsoft.AspNetCore.Identity;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Enums;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Domain.DTO;

using UserIdentity.Application.Core.Roles.Queries;
using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Common;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.Users.Queries;

public record GetUserQuery : IBaseQuery
{
  public required string UserId { get; init; }
}

public class GetUserQueryHandler(
    UserManager<IdentityUser> userManager,
    IUserRepository userRepository,
    IGetItemsQueryHandler<GetRoleClaimsForRolesQuery, RoleClaimsForRolesViewModels> getRoleClaimsQueryHandler
    ) : IGetItemQueryHandler<GetUserQuery, UserViewModel>
{

  private readonly UserManager<IdentityUser> _userManager = userManager;
  private readonly IUserRepository _userRepository = userRepository;
  private readonly IGetItemsQueryHandler<GetRoleClaimsForRolesQuery, RoleClaimsForRolesViewModels> _getRoleClaimsQueryHandler = getRoleClaimsQueryHandler;

  public async Task<UserViewModel> GetItemAsync(GetUserQuery query, CancellationToken cancellationToken = default)
  {
    var user = await _userManager.FindByIdAsync(query.UserId) ?? throw new NoRecordException($"{query.UserId}", EntityTypes.USER.Description());

    var userDetails = await _userRepository.GetEntityItemAsync(query.UserId, cancellationToken);

    var userRoles = await _userManager.GetRolesAsync(user);

    var userRoleClaims = await _getRoleClaimsQueryHandler.GetItemsAsync(new GetRoleClaimsForRolesQuery { Roles = userRoles }, cancellationToken);

    var userDTO = new UserDTO
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

    userDTO.SetDTOAuditFields(userDetails);

    return new UserViewModel
    {
      User = userDTO
    };
  }
}
