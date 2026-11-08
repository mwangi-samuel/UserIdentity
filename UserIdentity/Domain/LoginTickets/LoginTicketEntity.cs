using System.ComponentModel.DataAnnotations;
using PolyzenKit.Domain.Entities;

namespace UserIdentity.Domain.LoginTickets;

public class LoginTicketEntity : BaseAuditableEntity<Guid>
{
    [MaxLength(LoginTicketLimits.TicketMaxLength)]
    public string Ticket { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public int AppId { get; set; }

    public DateTime Expires { get; set; }
}
