using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Persistence.Repositories.StepUps;

namespace UserIdentity.Application.Core.TwoFactor.Commands;

public record DeleteExpiredStepUpsCommand : IBaseCommand;

public class DeleteExpiredStepUpsCommandHandler(IStepUpRepository stepUpRepository, TimeProvider timeProvider)
    : IDeleteItemCommandHandler<DeleteExpiredStepUpsCommand, DeleteRecordViewModel>
{
    private readonly IStepUpRepository _stepUpRepository = stepUpRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<DeleteRecordViewModel> DeleteItemAsync(
        DeleteExpiredStepUpsCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        int deleted;

        do
        {
            deleted = await _stepUpRepository.DeleteEntityItemsExpiredBeforeAsync(now, CleanupBatch.Size, cancellationToken);
        } while (deleted == CleanupBatch.Size);

        return new DeleteRecordViewModel();
    }
}
