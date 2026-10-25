using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Persistence.Repositories;
using PolyzenKit.Presentation.Settings;

using UserIdentity.Application.Core.InviteCodes.ViewModels;
using UserIdentity.Application.Helpers;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Persistence.Repositories.InviteCodes;

namespace UserIdentity.Application.Core.InviteCodes.Commands;

public class CreateInviteCodeCommand : InviteCodeEntity, IBaseCommand
{
  [JsonIgnore]
  public string AppName { get; internal set; } = string.Empty;
}

public class CreateInviteCodeCommandHandler(
   IOptions<RoleSettings> roleSettingsOptions,
   RoleManager<IdentityRole> roleManager,
   IUnitOfWork unitOfWork,
   IInviteCodeRepository inviteCodeRepository,
   IMachineDateTime machineDateTime
  ) : BaseAuditableItemCommandHandler<long, InviteCodeEntity, IInviteCodeRepository>(unitOfWork, inviteCodeRepository, machineDateTime),
      ICreateItemCommandHandler<CreateInviteCodeCommand, InviteCodeViewModel>
{
  private readonly RoleSettings _roleSettings = roleSettingsOptions.Value;
  private readonly RoleManager<IdentityRole> _roleManager = roleManager;

  public async Task<InviteCodeViewModel> CreateItemAsync(CreateInviteCodeCommand command, string userId, CancellationToken cancellationToken = default)
  {
    var entity = (InviteCodeEntity)command;

    entity.Role = await ResolveRoleAsync(command.Role, command.AppName);

    await CreateEntityItemAsync(entity, userId, cancellationToken);

    return new InviteCodeViewModel
    {
      InviteCode = await _repository.GetEntityDtoByIdAsync(entity.Id, cancellationToken)
    };
  }

  private async Task<string?> ResolveRoleAsync(string? roleName, string appName)
  {
    if (string.IsNullOrWhiteSpace(roleName))
      return null;

    roleName = roleName.Trim();

    if (!AppRolesHelper.IsAppRole(roleName, appName)
      || AppRolesHelper.IsAppAdminRole(roleName, appName, _roleSettings.AdminRoles)
      || AppRolesHelper.IsGlobalAdminRole(roleName, _roleSettings.GlobalAdminRole))
      throw new InvalidDataException($"An invite can only grant a {appName.TrimAndLowerInvariant()}{ZenConstants.SERVICE_ROLE_SEPARATOR} role that isn't an admin role");

    var role = await _roleManager.FindByNameAsync(roleName)
      ?? throw new InvalidDataException($"The role {roleName} doesn't exist");

    return role.Name;
  }
}
