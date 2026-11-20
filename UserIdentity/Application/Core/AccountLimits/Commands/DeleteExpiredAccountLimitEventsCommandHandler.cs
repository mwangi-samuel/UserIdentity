using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Domain.AccountLimitEvents;
using UserIdentity.Persistence.Repositories.AccountLimitEvents;

namespace UserIdentity.Application.Core.AccountLimits.Commands;

public record DeleteExpiredAccountLimitEventsCommand : IBaseCommand;

public class DeleteExpiredAccountLimitEventsCommandHandler(
    IAccountLimitEventRepository accountLimitEventRepository,
    TimeProvider timeProvider
) : IDeleteItemCommandHandler<DeleteExpiredAccountLimitEventsCommand, DeleteRecordViewModel>
{
    private readonly IAccountLimitEventRepository _accountLimitEventRepository = accountLimitEventRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<DeleteRecordViewModel> DeleteItemAsync(
        DeleteExpiredAccountLimitEventsCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var createdBefore = _timeProvider.GetUtcNow().UtcDateTime - AccountLimitEventLimits.CountWindow;
        int deleted;

        do
        {
            deleted = await _accountLimitEventRepository.DeleteEntityItemsCreatedBeforeAsync(
                createdBefore,
                CleanupBatch.Size,
                cancellationToken
            );
        } while (deleted == CleanupBatch.Size);

        return new DeleteRecordViewModel();
    }
}
