using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using PolyzenKit.Domain.Entities;
using PolyzenKit.Domain.Entities.RegisteredApps;

namespace UserIdentity.Domain.InviteCodes;

public class InviteCodeEntity : BaseAuditableEntity<long>
{
    public string InviteCode { get; set; } = null!;

    public string UserEmail { get; set; } = null!;

    public bool Applied { get; set; }

    public string? Role { get; set; }

    public int AppId { get; set; }

    [JsonIgnore]
    [ForeignKey(nameof(AppId))]
    public virtual RegisteredAppEntity App { get; set; } = null!;
}
