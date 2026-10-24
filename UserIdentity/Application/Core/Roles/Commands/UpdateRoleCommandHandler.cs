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


public record UpdateRoleCommand : IBaseCommand
{
  public string RoleId { get; internal set; } = string.Empty;

  [Required]
  public string RoleName { get; init; } = null!;

  [JsonIgnore]
  public string? AppName { get; internal set; }
}
public class UpdateRoleCommandHandler(
    RoleManager<IdentityRole> roleManager
    ) : IUpdateItemCommandHandler<UpdateRoleCommand, RoleViewModel>
{
  private readonly RoleManager<IdentityRole> _roleManager = roleManager;

  public async Task<RoleViewModel> UpdateItemAsync(UpdateRoleCommand command, string userId, CancellationToken cancellationToken = default)
  {
    var role = await _roleManager.FindByIdAsync(command.RoleId!) ?? throw new NoRecordException(command.RoleId, "Role");

    if (command.AppName is not null)
    {
      if (!AppRolesHelper.IsAppRole(role.Name, command.AppName))
        throw new NoRecordException(command.RoleId, "Role");

      if (!AppRolesHelper.IsAppRole(command.RoleName, command.AppName))
        throw new InvalidDataException($"Role names must start with {command.AppName.TrimAndLowerInvariant()}{ZenConstants.SERVICE_ROLE_SEPARATOR}");
    }

    role.Name = command.RoleName;

    var updateRoleResult = await _roleManager.UpdateAsync(role);

    if (!updateRoleResult.Succeeded)
      throw new RecordUpdateException(command.RoleId, "Role");

    return new RoleViewModel
    {
      Role = new RoleDto
      {
        Id = role.Id,
        Name = role.Name
      }
    };

  }
}
