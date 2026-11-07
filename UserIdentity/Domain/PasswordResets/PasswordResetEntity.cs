using System.ComponentModel.DataAnnotations;
using PolyzenKit.Domain.Entities;

namespace UserIdentity.Domain.PasswordResets;

public class PasswordResetEntity : BaseAuditableEntity<Guid>
{
    [MaxLength(PasswordResetLimits.RequestIdMaxLength)]
    public string RequestId { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public DateTime Expires { get; set; }

    public bool Verified { get; set; }

    public bool Applied { get; set; }
}
