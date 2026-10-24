using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Identity;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Utilities;

using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.Application.Helpers;
using UserIdentity.Domain.Roles;

namespace UserIdentity.Application.Core.Roles.Commands;

public record CreateRoleCommand : IBaseCommand
{
  [Required]
  public string RoleName { get; init; } = null!;

  [JsonIgnore]
  public string? AppName { get; internal set; }
}



public class CreateRoleCommandHandler(
    RoleManager<IdentityRole> roleManager
    ) : ICreateItemCommandHandler<CreateRoleCommand, RoleViewModel>
{
  private readonly RoleManager<IdentityRole> _roleManager = roleManager;

  public async Task<RoleViewModel> CreateItemAsync(CreateRoleCommand command, string userid, CancellationToken cancellationToken = default)
  {
    if (command.AppName is not null && !AppRolesHelper.IsAppRole(command.RoleName, command.AppName))
      throw new InvalidDataException($"Role names must start with {command.AppName.TrimAndLowerInvariant()}{ZenConstants.SERVICE_ROLE_SEPARATOR}");

    var existingRole = await _roleManager.FindByNameAsync(command.RoleName);

    if (existingRole != null)
      throw new RecordExistsException(command.RoleName, "Role");

    var newRole = new IdentityRole { Name = command.RoleName };

    var createdRoleResult = await _roleManager.CreateAsync(newRole);

    if (!createdRoleResult.Succeeded)
      throw new RecordCreationException(command.RoleName, "Role");

    return new RoleViewModel
    {
      Role = new RoleDto
      {
        Id = newRole.Id,
        Name = newRole.Name
      }
    };
  }
}
