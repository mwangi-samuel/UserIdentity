using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Extensions;
using UserIdentity.Application.Core.Roles.Shared;

namespace UserIdentity.Application.Core.Roles.Commands;

public record DeleteRoleClaimCommand : IBaseCommand
{
    [Required]
    public string RoleId { get; init; } = null!;

    [Required]
    public string Resource { get; init; } = null!;

    [Required]
    public string Action { get; init; } = null!;

    [JsonIgnore]
    public string? AppName { get; internal set; }
}

public class DeleteRoleClaimCommandHandler(RoleManager<IdentityRole> roleManager, IJwtTokenHandler jwtTokenHandler)
    : IDeleteItemCommandHandler<DeleteRoleClaimCommand, DeleteRecordViewModel>
{
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;
    private readonly IJwtTokenHandler _jwtTokenHandler = jwtTokenHandler;

    public async Task<DeleteRecordViewModel> DeleteItemAsync(
        DeleteRoleClaimCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        // Confirm role exists
        var role = await _roleManager.FindByIdAsync(command.RoleId) ?? throw NoRecordException.ForIdentifier(command.RoleId);

        if (command.AppName is not null && !AppRoleNames.IsAppRole(role.Name, command.AppName))
            throw NoRecordException.ForIdentifier(command.RoleId);

        // Scopes are stored in lower case, and the claim is removed by its exact value
        var scopeClaim = _jwtTokenHandler.GenerateScopeClaim(
            command.Resource.TrimAndLowerInvariant(),
            command.Action.TrimAndLowerInvariant()
        );
        var roleClaims = await _roleManager.GetClaimsAsync(role);

        if (!roleClaims.Any(roleClaim => roleClaim.Value == scopeClaim.Value))
            throw NoRecordException.ForIdentifier($"{command.Resource}:{command.Action}");

        // Delete Claim
        var deleteRoleClaimResults = await _roleManager.RemoveClaimAsync(role, scopeClaim);

        if (!deleteRoleClaimResults.Succeeded)
            throw new RecordDeletionException($"Scope {command.Resource}:{command.Action} wasn't removed from role {role.Id}");

        return new DeleteRecordViewModel { };
    }
}
