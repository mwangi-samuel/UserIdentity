using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Infrastructure.Security.Jwt;
using PolyzenKit.Presentation.Settings;

using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.Application.Helpers;
using UserIdentity.Domain.RoleClaims;

namespace UserIdentity.Application.Core.Roles.Commands;

public record CreateRoleClaimCommand : IBaseCommand
{
  public const string ActionPattern = $"(?i)^({ZenConstants.SCOPE_ACTION_CREATE}|{ZenConstants.SCOPE_ACTION_READ}|{ZenConstants.SCOPE_ACTION_EDIT}|{ZenConstants.SCOPE_ACTION_DELETE}|{ZenConstants.SCOPE_ACTION_DOWNLOAD}|{ZenConstants.SCOPE_ACTION_ALL})$";

  [Required]
  public string RoleId { get; init; } = null!;

  [Required]
  public string Resource { get; init; } = null!;

  [Required]
  [RegularExpression(ActionPattern, ErrorMessage = "Action must be create, read, edit, delete, download or all")]
  public string Action { get; set; } = null!;

  [JsonIgnore]
  public string? AppName { get; internal set; }
}

public class CreateRoleClaimCommandHandler(
    RoleManager<IdentityRole> roleManager,
    IJwtTokenHandler jwtTokenHandler,
    IOptions<RoleSettings> roleSettingsOptions
    ) : ICreateItemCommandHandler<CreateRoleClaimCommand, RoleClaimViewModel>
{
  private readonly RoleManager<IdentityRole> _roleManager = roleManager;
  private readonly IJwtTokenHandler _jwtTokenHandler = jwtTokenHandler;
  private readonly RoleSettings _roleSettings = roleSettingsOptions.Value;

  public async Task<RoleClaimViewModel> CreateItemAsync(CreateRoleClaimCommand command, string userId, CancellationToken cancellationToken = default)
  {
    // Confirm role exists
    var role = await _roleManager.FindByIdAsync(command.RoleId) ?? throw new NoRecordException(command.RoleId, "Role");

    if (command.AppName is not null && !AppRolesHelper.IsAppRole(role.Name, command.AppName))
      throw new NoRecordException(command.RoleId, "Role");

    // The global admin role is in every app's tokens, so its scopes would apply in every app
    if (AppRolesHelper.IsGlobalAdminRole(role.Name, _roleSettings.GlobalAdminRole))
      throw new InvalidDataException("Scopes can't be added to the global admin role");

    // Services compare scopes case-sensitively against lowercase service, controller and action names
    var resource = command.Resource.TrimAndLowerInvariant();
    var action = command.Action.TrimAndLowerInvariant();

    // Confirm claim does not exist
    var scopeClaim = _jwtTokenHandler.GenerateScopeClaim(resource, action);

    var roleClaims = await _roleManager.GetClaimsAsync(role);

    foreach (var roleClaim in roleClaims)
      if (roleClaim.Value == scopeClaim.Value)
        throw new RecordExistsException(scopeClaim.Value, "Role Claim");

    var roleClaimCreateResults = await _roleManager.AddClaimAsync(role, scopeClaim);

    if (!roleClaimCreateResults.Succeeded)
      throw new RecordCreationException(command.ToString(), "RoleClaim");

    return new RoleClaimViewModel
    {
      RoleClaim = new RoleClaimDto
      {
        Resource = resource,
        Action = action,
        Scope = scopeClaim.Value,
      }

    };
  }

}
