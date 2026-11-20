using System.ComponentModel.DataAnnotations;
using PolyzenKit.Domain.Entities;

namespace UserIdentity.Domain.EmailChanges;

public class EmailChangeEntity : BaseAuditableEntity<Guid>
{
    public string UserId { get; set; } = null!;

    [MaxLength(EmailChangeLimits.NewEmailMaxLength)]
    public string NewEmail { get; set; } = null!;

    public DateTime Expires { get; set; }
}
