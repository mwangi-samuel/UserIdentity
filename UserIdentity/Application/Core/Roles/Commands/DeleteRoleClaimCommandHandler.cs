using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Extensions;
using UserIdentity.Application.Core.Roles.Shared;

namespace UserIdentity.Application.Core.Roles.Commands;

public record DeleteRoleClaimCommand : RoleClaimInput, IBaseCommand
{
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
        var role = await AppRoleNames.GetAppRoleAsync(_roleManager, command.RoleId, command.AppName);

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
