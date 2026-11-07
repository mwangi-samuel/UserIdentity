using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Settings;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.InviteCodes.ViewModels;
using UserIdentity.Application.Core.Roles.Shared;
using UserIdentity.Common.Errors;
using UserIdentity.Common.Validation;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Persistence.Repositories.InviteCodes;

namespace UserIdentity.Application.Core.InviteCodes.Commands;

public record CreateInviteCodeCommand : IBaseCommand
{
    [
        Required,
        StringLength(InviteCodeLimits.UserEmailMaxLength),
        RegularExpression(ValidationPatterns.Email, ErrorMessage = ErrorMessages.Users.InvalidEmail)
    ]
    public string UserEmail { get; init; } = null!;

    [Required, StringLength(InviteCodeLimits.InviteCodeMaxLength, MinimumLength = InviteCodeLimits.InviteCodeMinLength)]
    public string InviteCode { get; init; } = null!;

    [StringLength(InviteCodeLimits.RoleMaxLength)]
    public string? Role { get; init; }

    [JsonIgnore]
    public int AppId { get; internal set; }

    [JsonIgnore]
    public string AppName { get; internal set; } = string.Empty;
}

public class CreateInviteCodeCommandHandler(
    IOptions<RoleSettings> roleSettingsOptions,
    RoleManager<IdentityRole> roleManager,
    IUnitOfWork unitOfWork,
    IInviteCodeRepository inviteCodeRepository,
    TimeProvider timeProvider
)
    : BaseAuditableItemCommandHandler<long, InviteCodeEntity, IInviteCodeRepository>(unitOfWork, inviteCodeRepository, timeProvider),
        ICreateItemCommandHandler<CreateInviteCodeCommand, InviteCodeViewModel>
{
    private readonly RoleSettings _roleSettings = roleSettingsOptions.Value;
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;

    public async Task<InviteCodeViewModel> CreateItemAsync(
        CreateInviteCodeCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var entity = new InviteCodeEntity
        {
            UserEmail = command.UserEmail,
            InviteCode = command.InviteCode,
            Role = await ResolveRoleAsync(command.Role, command.AppName),
            AppId = command.AppId,
        };

        await CreateEntityItemAsync(entity, userId, cancellationToken);

        return new InviteCodeViewModel { InviteCode = await Repository.GetEntityDtoByIdAsync(entity.Id, cancellationToken) };
    }

    private async Task<string?> ResolveRoleAsync(string? roleName, string appName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
            return null;

        roleName = roleName.Trim();

        if (
            !AppRoleNames.IsAppRole(roleName, appName)
            || AppRoleNames.IsAppAdminRole(roleName, appName, _roleSettings)
            || AppRoleNames.IsGlobalAdminRole(roleName, _roleSettings.GlobalAdminRole)
        )
            throw new InvalidDataException(ErrorMessages.InviteCodes.RoleNotGrantable(AppRoleNames.Prefix(appName)));

        var role =
            await _roleManager.FindByNameAsync(roleName) ?? throw new InvalidDataException(ErrorMessages.InviteCodes.RoleMissing(roleName));

        return role.Name;
    }
}
