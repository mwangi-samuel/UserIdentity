using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Extensions;
using UserIdentity.Application.Core.Roles.Shared;
using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.Roles;

namespace UserIdentity.Application.Core.Roles.Commands;

public record CreateRoleCommand : RoleInput, IBaseCommand
{
    [JsonIgnore]
    public string? AppName { get; internal set; }
}

public class CreateRoleCommandHandler(RoleManager<IdentityRole> roleManager) : ICreateItemCommandHandler<CreateRoleCommand, RoleViewModel>
{
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;

    public async Task<RoleViewModel> CreateItemAsync(
        CreateRoleCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var roleName = command.RoleName.TrimAndLowerInvariant();

        if (command.AppName is not null && !AppRoleNames.IsAppRole(roleName, command.AppName))
            throw new InvalidDataException(ErrorMessages.Roles.NameOutsideApp(AppRoleNames.Prefix(command.AppName)));

        var existingRole = await _roleManager.FindByNameAsync(roleName);

        if (existingRole != null)
            throw RecordExistsException.ForIdentifier(roleName);

        var newRole = new IdentityRole { Name = roleName };

        var createdRoleResult = await _roleManager.CreateAsync(newRole);

        if (!createdRoleResult.Succeeded)
            throw new RecordCreationException($"Role {roleName} wasn't created");

        return new RoleViewModel
        {
            Role = new RoleDto { Id = newRole.Id, Name = newRole.Name },
        };
    }
}
