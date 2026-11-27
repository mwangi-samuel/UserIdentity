using System.ComponentModel.DataAnnotations;
using PolyzenKit.Domain.Entities;

namespace UserIdentity.Domain.AppSignInSettings;

public class AppSignInSettingsEntity : BaseAuditableEntity<Guid>
{
    public int AppId { get; set; }

    public bool RequireConfirmedAccount { get; set; }

    public bool RequireInviteCode { get; set; }

    [MaxLength(AppSignInSettingsLimits.DefaultRoleMaxLength)]
    public string? DefaultRole { get; set; }

    public bool RequireTwoFactor { get; set; }

    public bool RequireTwoFactorForAdmins { get; set; }

    public bool AllowAuthenticatorTwoFactor { get; set; }

    public bool AllowEmailTwoFactor { get; set; }

    public bool AllowSmsTwoFactor { get; set; }

    [ConcurrencyCheck]
    public Guid Version { get; set; }
}
