using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core;
using UserIdentity.Domain.LoginTickets;

namespace UserIdentity.Application.Core.TwoFactor.ViewModels;

public record SecondStepTicketViewModel : BaseViewModel
{
    public required LoginTicketEntity LoginTicket { get; init; }

    public required IdentityUser User { get; init; }
}
