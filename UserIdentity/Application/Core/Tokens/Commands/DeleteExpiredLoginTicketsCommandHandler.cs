using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using UserIdentity.Persistence.Repositories.LoginTickets;

namespace UserIdentity.Application.Core.Tokens.Commands;

public record DeleteExpiredLoginTicketsCommand : IBaseCommand;

public class DeleteExpiredLoginTicketsCommandHandler(ILoginTicketRepository loginTicketRepository, TimeProvider timeProvider)
    : IDeleteItemCommandHandler<DeleteExpiredLoginTicketsCommand, DeleteRecordViewModel>
{
    public const int BatchSize = 100;

    private readonly ILoginTicketRepository _loginTicketRepository = loginTicketRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<DeleteRecordViewModel> DeleteItemAsync(
        DeleteExpiredLoginTicketsCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        int deleted;

        do
        {
            deleted = await _loginTicketRepository.DeleteEntityItemsExpiredBeforeAsync(now, BatchSize, cancellationToken);
        } while (deleted == BatchSize);

        return new DeleteRecordViewModel();
    }
}
