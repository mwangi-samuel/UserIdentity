using PolyzenKit.Application.Core;

namespace UserIdentity.Application.Core.Tokens.ViewModels;

public record LoginTicketViewModel : BaseViewModel
{
    public string Ticket { get; init; } = null!;
}
