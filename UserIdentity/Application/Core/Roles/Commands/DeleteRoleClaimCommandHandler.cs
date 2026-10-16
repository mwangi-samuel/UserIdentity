using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Identity;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Infrastructure.Security.Jwt;

namespace UserIdentity.Application.Core.Roles.Commands;

public record DeleteRoleClaimCommand : IBaseCommand
{
  [Required]
  public string RoleId { get; init; } = null!;

  [Required]
  public string Resource { get; init; } = null!;

  [Required]
  public string Action { get; init; } = null!;
}
public class DeleteRoleClaimCommandHandler(
    RoleManager<IdentityRole> roleManager,
    IJwtTokenHandler jwtTokenHandler
    ) : IDeleteItemCommandHandler<DeleteRoleClaimCommand, DeleteRecordViewModel>
{
  private readonly RoleManager<IdentityRole> _roleManager = roleManager;
  private readonly IJwtTokenHandler _jwtTokenHandler = jwtTokenHandler;

  public async Task<DeleteRecordViewModel> DeleteItemAsync(DeleteRoleClaimCommand command, string userId, CancellationToken cancellationToken = default)
  {
    // Confirm role exists
    var role = await _roleManager.FindByIdAsync(command.RoleId) ?? throw new NoRecordException(command.RoleId, "Role");

    // Confirm claim does not exist 
    var scopeClaim = _jwtTokenHandler.GenerateScopeClaim(command.Resource, command.Action);
    var roleClaims = await _roleManager.GetClaimsAsync(role);

    var claimFound = false;

    foreach (var roleClaim in roleClaims)
    {
      if (string.Equals(roleClaim.Value, scopeClaim.Value, StringComparison.OrdinalIgnoreCase))
      {
        claimFound = true;
        break;
      }
    }

    if (!claimFound)
      throw new NoRecordException($"{command.Resource}:{command.Action}", "RoleClaim");

    // Delete Claim
    var deleteRoleClaimResults = await _roleManager.RemoveClaimAsync(role, scopeClaim);

    if (!deleteRoleClaimResults.Succeeded)
      throw new RecordDeletionException($"{command.Resource}:{command.Action}", "RoleClaim");

    return new DeleteRecordViewModel
    {

    };
  }
}
