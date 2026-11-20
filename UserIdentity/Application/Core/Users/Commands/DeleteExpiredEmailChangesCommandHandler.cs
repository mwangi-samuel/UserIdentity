using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Persistence.Repositories.EmailChanges;

namespace UserIdentity.Application.Core.Users.Commands;

public record DeleteExpiredEmailChangesCommand : IBaseCommand;

public class DeleteExpiredEmailChangesCommandHandler(IEmailChangeRepository emailChangeRepository, TimeProvider timeProvider)
    : IDeleteItemCommandHandler<DeleteExpiredEmailChangesCommand, DeleteRecordViewModel>
{
    private readonly IEmailChangeRepository _emailChangeRepository = emailChangeRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<DeleteRecordViewModel> DeleteItemAsync(
        DeleteExpiredEmailChangesCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        int deleted;

        do
        {
            deleted = await _emailChangeRepository.DeleteEntityItemsExpiredBeforeAsync(now, CleanupBatch.Size, cancellationToken);
        } while (deleted == CleanupBatch.Size);

        return new DeleteRecordViewModel();
    }
}
