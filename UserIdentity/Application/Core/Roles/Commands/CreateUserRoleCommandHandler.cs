using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Identity;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;

using UserIdentity.Application.Core.Roles.Queries;
using UserIdentity.Application.Core.Roles.Shared;
using UserIdentity.Application.Core.Roles.ViewModels;
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
  private readonly IGetItemsQueryHandler<GetUserRolesQuery, UserRolesViewModel> _getUserRolesQueryHandler =
    getUserRolesQueryHandler;

  public async Task<UserRolesViewModel> CreateItemAsync(
    CreateUserRoleCommand command,
    string userId,
    CancellationToken cancellationToken = default)
  {
    var user = await _userManager.FindByIdAsync(command.UserId)
      ?? throw NoRecordException.ForIdentifier(command.UserId);

    if (command.AppName is not null
      && !await _userRegisteredAppRepository.IsUserRegisteredForAppAsync(
        command.UserId,
        command.AppName,
        cancellationToken))
      throw NoRecordException.ForIdentifier(command.UserId);

    var role = await _roleManager.FindByIdAsync(command.RoleId)
      ?? throw NoRecordException.ForIdentifier(command.RoleId);

    if (command.AppName is not null && !AppRoleNames.IsAppRole(role.Name, command.AppName))
      throw NoRecordException.ForIdentifier(command.RoleId);

    var userRoleExists = await _userManager.IsInRoleAsync(user, role.Name!);

    if (userRoleExists)
      throw RecordExistsException.ForIdentifier(command.RoleId);

    var resultUserRole = await _userManager.AddToRoleAsync(user, role.Name!);

    if (!resultUserRole.Succeeded)
      throw new RecordCreationException($"Role {command.RoleId} wasn't added to user {command.UserId}");

    // remove roles from cache

    var resolvedRoles = await _getUserRolesQueryHandler.GetItemsAsync(
      new GetUserRolesQuery { UserId = command.UserId, AppName = command.AppName },
      cancellationToken);

    return resolvedRoles;
  }
}
