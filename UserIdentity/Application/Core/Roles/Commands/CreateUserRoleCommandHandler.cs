using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Identity;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;

using UserIdentity.Application.Core.Roles.Queries;
using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.Application.Helpers;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;

namespace UserIdentity.Application.Core.Roles.Commands;

public record CreateUserRoleCommand : IBaseCommand
{
  [Required]
  public string UserId { get; init; } = null!;

  [Required]
  public string RoleId { get; init; } = null!;

  [JsonIgnore]
  public string? AppName { get; internal set; }
}

public class CreateUserRoleCommandHandler(
    RoleManager<IdentityRole> roleManager,
    UserManager<IdentityUser> userManager,
    IUserRegisteredAppRepository userRegisteredAppRepository,
    IGetItemsQueryHandler<GetUserRolesQuery, UserRolesViewModel> getUserRolesQueryHandler
    ) : ICreateItemCommandHandler<CreateUserRoleCommand, UserRolesViewModel>
{
  private readonly RoleManager<IdentityRole> _roleManager = roleManager;
  private readonly UserManager<IdentityUser> _userManager = userManager;
  private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = userRegisteredAppRepository;
  private readonly IGetItemsQueryHandler<GetUserRolesQuery, UserRolesViewModel> _getUserRolesQueryHandler = getUserRolesQueryHandler;

  public async Task<UserRolesViewModel> CreateItemAsync(CreateUserRoleCommand command, string userid, CancellationToken cancellationToken = default)
  {
    var user = await _userManager.FindByIdAsync(command.UserId) ?? throw new NoRecordException(command.UserId + "", "User");

    if (command.AppName is not null && !await _userRegisteredAppRepository.IsUserRegisteredForAppAsync(command.UserId, command.AppName, cancellationToken))
      throw new NoRecordException(command.UserId + "", "User");

    var role = await _roleManager.FindByIdAsync(command.RoleId) ?? throw new NoRecordException(command.RoleId + "", "Role");

    if (command.AppName is not null && !AppRolesHelper.IsAppRole(role.Name, command.AppName))
      throw new NoRecordException(command.RoleId + "", "Role");

    var userRoleExists = await _userManager.IsInRoleAsync(user, role.Name!);

    if (userRoleExists)
      throw new RecordExistsException(command.RoleId, "UserRole");

    var resultUserRole = await _userManager.AddToRoleAsync(user, role.Name!);

    if (!resultUserRole.Succeeded)
      throw new RecordCreationException(command.RoleId, "UserRole");

    // remove roles from cache

    var resolvedRoles = await _getUserRolesQueryHandler.GetItemsAsync(new GetUserRolesQuery { UserId = command.UserId, AppName = command.AppName }, cancellationToken);

    return resolvedRoles;
  }
}
