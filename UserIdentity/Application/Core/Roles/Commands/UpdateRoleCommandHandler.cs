using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Identity;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Extensions;

using UserIdentity.Application.Core.Roles.Shared;
using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.Common.Errors;
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

  public async Task<RoleViewModel> UpdateItemAsync(
    UpdateRoleCommand command,
    string userId,
    CancellationToken cancellationToken = default)
  {
    var role = await _roleManager.FindByIdAsync(command.RoleId!)
      ?? throw NoRecordException.ForIdentifier(command.RoleId);

    var roleName = command.RoleName.TrimAndLowerInvariant();

    if (command.AppName is not null)
    {
      if (!AppRoleNames.IsAppRole(role.Name, command.AppName))
        throw NoRecordException.ForIdentifier(command.RoleId);

      if (!AppRoleNames.IsAppRole(roleName, command.AppName))
        throw new InvalidDataException(ErrorMessages.Roles.NameOutsideApp(AppRoleNames.Prefix(command.AppName)));
    }

    role.Name = roleName;

    var updateRoleResult = await _roleManager.UpdateAsync(role);

    if (!updateRoleResult.Succeeded)
      throw new RecordUpdateException($"Role {command.RoleId} wasn't updated");

    return new RoleViewModel { Role = new RoleDto { Id = role.Id, Name = role.Name } };

  }
}
