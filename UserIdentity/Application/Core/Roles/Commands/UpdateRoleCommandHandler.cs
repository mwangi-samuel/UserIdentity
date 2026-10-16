using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Identity;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;

using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.Domain.Roles;

namespace UserIdentity.Application.Core.Roles.Commands;


public record UpdateRoleCommand : IBaseCommand
{
  public string RoleId { get; internal set; } = string.Empty;

  [Required]
  public string RoleName { get; init; } = null!;
}
public class UpdateRoleCommandHandler(
    RoleManager<IdentityRole> roleManager
    ) : IUpdateItemCommandHandler<UpdateRoleCommand, RoleViewModel>
{
  private readonly RoleManager<IdentityRole> _roleManager = roleManager;

  public async Task<RoleViewModel> UpdateItemAsync(UpdateRoleCommand command, string userId, CancellationToken cancellationToken = default)
  {
    var role = await _roleManager.FindByIdAsync(command.RoleId!) ?? throw new NoRecordException(command.RoleId, "Role");

    role.Name = command.RoleName;

    var updateRoleResult = await _roleManager.UpdateAsync(role);

    if (!updateRoleResult.Succeeded)
      throw new RecordUpdateException(command.RoleId, "Role");

    return new RoleViewModel
    {
      Role = new RoleDTO
      {
        Id = role.Id,
        Name = role.Name
      }
    };

  }
}
