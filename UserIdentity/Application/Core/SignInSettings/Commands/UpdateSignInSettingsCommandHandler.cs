using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Extensions;
using PolyzenKit.Common.Settings;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.Roles.Shared;
using UserIdentity.Application.Core.SignInSettings.ViewModels;
using UserIdentity.Common.Errors;
using UserIdentity.Common.Validation;
using UserIdentity.Domain.AppSignInSettings;
using UserIdentity.Persistence.Repositories.AppSignInSettings;

namespace UserIdentity.Application.Core.SignInSettings.Commands;

public record UpdateSignInSettingsCommand : IBaseCommand, IValidatableObject
{
    [JsonIgnore]
    public RegisteredAppDto RegisteredApp { get; internal set; } = null!;

    public bool RequireConfirmedAccount { get; init; }

    public bool RequireInviteCode { get; init; }

    [StringLength(AppSignInSettingsLimits.DefaultRoleMaxLength, MinimumLength = 1)]
    [RegularExpression(ValidationPatterns.DefaultRole, ErrorMessage = ErrorMessages.SignInSettings.DefaultRoleFormat)]
    public string? DefaultRole { get; init; }

    public bool RequireTwoFactor { get; init; }

    public bool RequireTwoFactorForAdmins { get; init; }

    public bool AllowAuthenticatorTwoFactor { get; init; }

    public bool AllowEmailTwoFactor { get; init; }

    public bool AllowSmsTwoFactor { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if ((RequireTwoFactor || RequireTwoFactorForAdmins) && !AllowAuthenticatorTwoFactor && !AllowEmailTwoFactor && !AllowSmsTwoFactor)
            yield return new ValidationResult(
                ErrorMessages.SignInSettings.TwoFactorWithoutMethod,
                [nameof(RequireTwoFactor), nameof(RequireTwoFactorForAdmins)]
            );
    }
}

public class UpdateSignInSettingsCommandHandler(
    IOptions<RoleSettings> roleSettingsOptions,
    IUnitOfWork unitOfWork,
    IAppSignInSettingsRepository appSignInSettingsRepository,
    TimeProvider timeProvider
)
    : BaseAuditableItemCommandHandler<Guid, AppSignInSettingsEntity, IAppSignInSettingsRepository>(
        unitOfWork,
        appSignInSettingsRepository,
        timeProvider
    ),
        IUpdateItemCommandHandler<UpdateSignInSettingsCommand, SignInSettingsViewModel>
{
    private readonly RoleSettings _roleSettings = roleSettingsOptions.Value;

    public async Task<SignInSettingsViewModel> UpdateItemAsync(
        UpdateSignInSettingsCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var appName = command.RegisteredApp.AppName;
        var defaultRole = command.DefaultRole?.TrimAndLowerInvariant();

        if (defaultRole is not null && AppRoleNames.IsAppAdminRole($"{AppRoleNames.Prefix(appName)}{defaultRole}", appName, _roleSettings))
            throw new InvalidDataException(ErrorMessages.SignInSettings.AdminDefaultRole(command.DefaultRole!));

        var entity = await Repository.GetEntityByAlternateIdAsync(
            new AppSignInSettingsEntity { AppId = command.RegisteredApp.Id },
            QueryCondition.MayOrMayNotExist,
            cancellationToken
        );

        var isNew = entity is null;

        entity ??= new AppSignInSettingsEntity { AppId = command.RegisteredApp.Id };
        entity.RequireConfirmedAccount = command.RequireConfirmedAccount;
        entity.RequireInviteCode = command.RequireInviteCode;
        entity.DefaultRole = defaultRole;
        entity.RequireTwoFactor = command.RequireTwoFactor;
        entity.RequireTwoFactorForAdmins = command.RequireTwoFactorForAdmins;
        entity.AllowAuthenticatorTwoFactor = command.AllowAuthenticatorTwoFactor;
        entity.AllowEmailTwoFactor = command.AllowEmailTwoFactor;
        entity.AllowSmsTwoFactor = command.AllowSmsTwoFactor;

        if (isNew)
            await CreateEntityItemAsync(entity, userId, cancellationToken);
        else
            await UpdateEntityItemAsync(entity, userId, cancellationToken);

        return new SignInSettingsViewModel { SignInSettings = AppSignInSettingsDto.FromEntityDetailed(entity) };
    }
}
