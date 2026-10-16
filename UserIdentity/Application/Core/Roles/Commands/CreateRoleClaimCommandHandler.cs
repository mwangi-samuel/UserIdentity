using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Identity;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Infrastructure.Security.Jwt;

using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.Domain.RoleClaims;

namespace UserIdentity.Application.Core.Roles.Commands;

public record CreateRoleClaimCommand : IBaseCommand
{
  [Required]
  public string RoleId { get; init; } = null!;

  [Required]
  public string Resource { get; init; } = null!;

  [Required]
  public string Action { get; set; } = null!;
}

public class CreateRoleClaimCommandHandler(
    RoleManager<IdentityRole> roleManager,
    IJwtTokenHandler jwtTokenHandler
    ) : ICreateItemCommandHandler<CreateRoleClaimCommand, RoleClaimViewModel>
{
  private readonly RoleManager<IdentityRole> _roleManager = roleManager;
  private readonly IJwtTokenHandler _jwtTokenHandler = jwtTokenHandler;

  public async Task<RoleClaimViewModel> CreateItemAsync(CreateRoleClaimCommand command, string userId, CancellationToken cancellationToken = default)
  {
    // Confirm role exists
    var role = await _roleManager.FindByIdAsync(command.RoleId) ?? throw new NoRecordException(command.RoleId, "Role");

    // Confirm claim does not exist 
    var scopeClaim = _jwtTokenHandler.GenerateScopeClaim(command.Resource, command.Action);

    var roleClaims = await _roleManager.GetClaimsAsync(role);

    foreach (var roleClaim in roleClaims)
      if (roleClaim.Value == scopeClaim.Value)
        throw new RecordExistsException(scopeClaim.Value, "Role Claim");

    var roleClaimCreateResults = await _roleManager.AddClaimAsync(role, scopeClaim);

    if (!roleClaimCreateResults.Succeeded)
      throw new RecordCreationException(command.ToString(), "RoleClaim");

    return new RoleClaimViewModel
    {
      RoleClaim = new RoleClaimDTO
      {
        Resource = command.Resource,
        Action = command.Action,
        Scope = scopeClaim.Value,
      }

    };
  }

}
