using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Extensions;
using PolyzenKit.Common.Settings;
using UserIdentity.Application.Core.Roles.Shared;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Common.Errors;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;

namespace UserIdentity.Application.Core.Roles.Commands;

public record DeleteUserRoleCommand : IBaseCommand
{
    public required string UserId { get; init; }

    public required string RoleId { get; init; }

    public string? AppName { get; init; }

    public required UserChanger Changer { get; init; }
}

public class DeleteUserRoleCommandHandler(
    IOptions<RoleSettings> roleSettingsOptions,
    RoleManager<IdentityRole> roleManager,
    UserManager<IdentityUser> userManager,
    IUserRegisteredAppRepository userRegisteredAppRepository
) : IDeleteItemCommandHandler<DeleteUserRoleCommand, DeleteRecordViewModel>
{
    private readonly RoleSettings _roleSettings = roleSettingsOptions.Value;
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = userRegisteredAppRepository;

    public async Task<DeleteRecordViewModel> DeleteItemAsync(
        DeleteUserRoleCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var user = await _userManager.FindByIdAsync(command.UserId) ?? throw NoRecordException.ForIdentifier(command.UserId);

        if (command.AppName is not null)
            await UserChangeAccess.EnsureAllowedAsync(
                _userManager,
                _userRegisteredAppRepository,
                _roleSettings,
                user,
                command.UserId,
                command.AppName,
                command.Changer,
                cancellationToken
            );

        var role = await AppRoleNames.GetAppRoleAsync(_roleManager, command.RoleId, command.AppName);

        if (!await _userManager.IsInRoleAsync(user, role.Name!))
            throw NoRecordException.ForIdentifier(command.RoleId);

        var globalAdminRole = _roleSettings.GlobalAdminRole.TrimAndLowerInvariant();

        if (
            string.Equals(role.Name, globalAdminRole, StringComparison.OrdinalIgnoreCase)
            && (await _userManager.GetUsersInRoleAsync(globalAdminRole)).Count == 1
        )
            throw new IllegalOperationException(ErrorMessages.Roles.LastGlobalAdminRole);

        var removeResult = await _userManager.RemoveFromRoleAsync(user, role.Name!);

        if (!removeResult.Succeeded)
            throw new RecordDeletionException($"Role {command.RoleId} wasn't taken from user {command.UserId}");

        return new DeleteRecordViewModel { };
    }
}
