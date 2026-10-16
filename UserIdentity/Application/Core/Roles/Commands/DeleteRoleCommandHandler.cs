using Microsoft.AspNetCore.Identity;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;

namespace UserIdentity.Application.Core.Roles.Commands;


public record DeleteRoleCommand : IBaseCommand
{
  public required string RoleId { get; init; }
}
public class DeleteRoleCommandHandler(
    RoleManager<IdentityRole> roleManager
    ) : IDeleteItemCommandHandler<DeleteRoleCommand, DeleteRecordViewModel>
{
  private readonly RoleManager<IdentityRole> _roleManager = roleManager;

  public async Task<DeleteRecordViewModel> DeleteItemAsync(DeleteRoleCommand command, string userId, CancellationToken cancellationToken = default)
  {
    var role = await _roleManager.FindByIdAsync(command.RoleId) ?? throw new NoRecordException(command.RoleId, "Role");

    var deleteRoleResult = await _roleManager.DeleteAsync(role);

    if (!deleteRoleResult.Succeeded)
      throw new RecordDeletionException(command.RoleId, "Role");

    return new DeleteRecordViewModel
    {

    };
  }
}
