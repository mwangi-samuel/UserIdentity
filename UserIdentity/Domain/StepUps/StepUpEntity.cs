using System.ComponentModel.DataAnnotations;
using PolyzenKit.Domain.Entities;

namespace UserIdentity.Domain.StepUps;

public class StepUpEntity : BaseAuditableEntity<Guid>
{
    public string UserId { get; set; } = null!;

    public int AppId { get; set; }

    [MaxLength(StepUpLimits.TicketMaxLength)]
    public string? Ticket { get; set; }

    public DateTime Expires { get; set; }
}
