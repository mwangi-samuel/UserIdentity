using System.ComponentModel.DataAnnotations;
using PolyzenKit.Domain.Entities;
using UserIdentity.Common.Enums;

namespace UserIdentity.Domain.LoginTickets;

public class LoginTicketEntity : BaseAuditableEntity<Guid>
{
    [MaxLength(LoginTicketLimits.TicketMaxLength)]
    public string Ticket { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public int AppId { get; set; }

    public DateTime Expires { get; set; }

    public SignInStep Step { get; set; }

    [MaxLength(LoginTicketLimits.CodeActionMaxLength)]
    public string? CodeAction { get; set; }

    [MaxLength(LoginTicketLimits.CodeCorrelationIdMaxLength)]
    public string? CodeCorrelationId { get; set; }

    public bool EmailProven { get; set; }

    public bool ChangingMethod { get; set; }

    public TwoFactorMethod? Method { get; set; }

    [MaxLength(LoginTicketLimits.PhoneNumberMaxLength)]
    public string? PhoneNumber { get; set; }
}
