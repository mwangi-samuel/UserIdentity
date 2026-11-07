using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using PolyzenKit.Domain.Entities;
using PolyzenKit.Domain.Entities.RegisteredApps;

namespace UserIdentity.Domain.InviteCodes;

public class InviteCodeEntity : BaseAuditableEntity<long>
{
    [MaxLength(InviteCodeLimits.InviteCodeMaxLength)]
    public string InviteCode { get; set; } = null!;

    [MaxLength(InviteCodeLimits.UserEmailMaxLength)]
    public string UserEmail { get; set; } = null!;

    public bool Applied { get; set; }

    [MaxLength(InviteCodeLimits.RoleMaxLength)]
    public string? Role { get; set; }

    public int AppId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(AppId))]
    public virtual RegisteredAppEntity App { get; set; } = null!;
}
