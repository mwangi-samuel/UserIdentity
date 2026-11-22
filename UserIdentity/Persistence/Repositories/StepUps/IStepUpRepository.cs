using PolyzenKit.Persistence.Repositories;
using UserIdentity.Domain.StepUps;

namespace UserIdentity.Persistence.Repositories.StepUps;

public interface IStepUpRepository : IEntityRepository<StepUpEntity, Guid>, IItemEntityRepository<StepUpEntity, Guid>
{
    Task<StepUpEntity?> GetPendingEntityAsync(
        string userId,
        int appId,
        DateTime expiresAfter,
        CancellationToken cancellationToken = default
    );

    Task<int> DeleteEntityItemsExpiredBeforeAsync(DateTime expiredBefore, int count, CancellationToken cancellationToken = default);
}
