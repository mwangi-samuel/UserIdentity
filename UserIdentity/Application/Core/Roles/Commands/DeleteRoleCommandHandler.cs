using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;
using UserIdentity.Application.Core.Roles.Shared;
using UserIdentity.Common.Errors;
using UserIdentity.Persistence.Repositories.InviteCodes;

namespace UserIdentity.Application.Core.Roles.Commands;

public record DeleteRoleCommand : IBaseCommand
{
    public required string RoleId { get; init; }

    public string? AppName { get; init; }
}

public class DeleteRoleCommandHandler(RoleManager<IdentityRole> roleManager, IInviteCodeRepository inviteCodeRepository)
    : IDeleteItemCommandHandler<DeleteRoleCommand, DeleteRecordViewModel>
{
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;
    private readonly IInviteCodeRepository _inviteCodeRepository = inviteCodeRepository;

    public async Task<DeleteRecordViewModel> DeleteItemAsync(
        DeleteRoleCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var role = await AppRoleNames.GetAppRoleAsync(_roleManager, command.RoleId, command.AppName);

        if ((await _inviteCodeRepository.GetUnusedEntityItemsByRoleAsync(role.Name!, cancellationToken)).Count > 0)
            throw new IllegalOperationException(ErrorMessages.Roles.GrantedByUnusedInvites);

        var deleteRoleResult = await _roleManager.DeleteAsync(role);

        if (!deleteRoleResult.Succeeded)
            throw new RecordDeletionException($"Role {command.RoleId} wasn't deleted");

        return new DeleteRecordViewModel { };
    }
}
