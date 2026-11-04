using Microsoft.AspNetCore.Identity;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;

using UserIdentity.Application.Core.Roles.Shared;

namespace UserIdentity.Application.Core.Roles.Commands;

public record DeleteRoleCommand : IBaseCommand
{
  public required string RoleId { get; init; }

  public string? AppName { get; init; }
}
public class DeleteRoleCommandHandler(
  RoleManager<IdentityRole> roleManager
  ) : IDeleteItemCommandHandler<DeleteRoleCommand, DeleteRecordViewModel>
{
  private readonly RoleManager<IdentityRole> _roleManager = roleManager;

  public async Task<DeleteRecordViewModel> DeleteItemAsync(
    DeleteRoleCommand command,
    string userId,
    CancellationToken cancellationToken = default)
  {
    var role = await _roleManager.FindByIdAsync(command.RoleId)
      ?? throw NoRecordException.ForIdentifier(command.RoleId);

    if (command.AppName is not null && !AppRoleNames.IsAppRole(role.Name, command.AppName))
      throw NoRecordException.ForIdentifier(command.RoleId);

    var deleteRoleResult = await _roleManager.DeleteAsync(role);

    if (!deleteRoleResult.Succeeded)
      throw new RecordDeletionException($"Role {command.RoleId} wasn't deleted");

    return new DeleteRecordViewModel { };
  }
}
