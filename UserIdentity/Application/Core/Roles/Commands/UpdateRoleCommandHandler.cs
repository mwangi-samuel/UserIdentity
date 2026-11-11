using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Extensions;
using PolyzenKit.Domain.Entities;
using UserIdentity.Application.Core.Roles.Shared;
using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.Roles;
using UserIdentity.Persistence.Repositories.InviteCodes;

namespace UserIdentity.Application.Core.Roles.Commands;

public record UpdateRoleCommand : RoleInput, IBaseCommand
{
    [JsonIgnore]
    public string RoleId { get; internal set; } = string.Empty;

    [JsonIgnore]
    public string? AppName { get; internal set; }
}

public class UpdateRoleCommandHandler(
    RoleManager<IdentityRole> roleManager,
    IInviteCodeRepository inviteCodeRepository,
    TimeProvider timeProvider
) : IUpdateItemCommandHandler<UpdateRoleCommand, RoleViewModel>
{
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;
    private readonly IInviteCodeRepository _inviteCodeRepository = inviteCodeRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<RoleViewModel> UpdateItemAsync(
        UpdateRoleCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var role = await AppRoleNames.GetAppRoleAsync(_roleManager, command.RoleId, command.AppName);

        var roleName = command.RoleName.TrimAndLowerInvariant();

        if (command.AppName is not null && !AppRoleNames.IsAppRole(roleName, command.AppName))
            throw new InvalidDataException(ErrorMessages.Roles.NameOutsideApp(AppRoleNames.Prefix(command.AppName)));

        foreach (var inviteCode in await _inviteCodeRepository.GetUnusedEntityItemsByRoleAsync(role.Name!, cancellationToken))
        {
            inviteCode.Role = roleName;
            inviteCode.UpdateEntityAuditFields(userId, _timeProvider.GetUtcNow().UtcDateTime);

            _inviteCodeRepository.UpdateEntityItem(inviteCode);
        }

        role.Name = roleName;

        var updateRoleResult = await _roleManager.UpdateAsync(role);

        if (!updateRoleResult.Succeeded)
            throw new RecordUpdateException($"Role {command.RoleId} wasn't updated");

        return new RoleViewModel
        {
            Role = new RoleDto { Id = role.Id, Name = role.Name },
        };
    }
}
