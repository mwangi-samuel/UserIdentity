using PolyzenKit.Application.Core;

namespace UserIdentity.Application.Core.Tokens.ViewModels;

public record LoginTicketViewModel : BaseViewModel
{
    public required string Ticket { get; init; }
}
