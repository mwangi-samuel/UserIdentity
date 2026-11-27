using System.Linq.Expressions;
using PolyzenKit.Domain.Dtos;

namespace UserIdentity.Domain.AppSignInSettings;

public class AppSignInSettingsDto : AppSignInSettingsEntity, IBaseAuditableEntityDto<Guid>
{
    public static Expression<Func<AppSignInSettingsEntity, AppSignInSettingsDto>> Detailed =>
        entity => new AppSignInSettingsDto
        {
            Id = entity.Id,
            AppId = entity.AppId,
            RequireConfirmedAccount = entity.RequireConfirmedAccount,
            RequireInviteCode = entity.RequireInviteCode,
            DefaultRole = entity.DefaultRole,
            RequireTwoFactor = entity.RequireTwoFactor,
            RequireTwoFactorForAdmins = entity.RequireTwoFactorForAdmins,
            AllowAuthenticatorTwoFactor = entity.AllowAuthenticatorTwoFactor,
            AllowEmailTwoFactor = entity.AllowEmailTwoFactor,
            AllowSmsTwoFactor = entity.AllowSmsTwoFactor,
            Version = entity.Version,
            CreatedBy = entity.CreatedBy,
            CreatedAt = entity.CreatedAt,
            UpdatedBy = entity.UpdatedBy,
            UpdatedAt = entity.UpdatedAt,
        };

    private static readonly Func<AppSignInSettingsEntity, AppSignInSettingsDto> _compiledDetailed = Detailed.Compile();

    public static AppSignInSettingsDto FromEntityDetailed(AppSignInSettingsEntity entity) => _compiledDetailed(entity);
}
