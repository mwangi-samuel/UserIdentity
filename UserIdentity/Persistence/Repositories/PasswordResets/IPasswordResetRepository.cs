using PolyzenKit.Persistence.Repositories;
using UserIdentity.Domain.PasswordResets;

namespace UserIdentity.Persistence.Repositories.PasswordResets;

public interface IPasswordResetRepository : IEntityRepository<PasswordResetEntity, Guid>, IItemEntityRepository<PasswordResetEntity, Guid>
{
    Task<(DateTime? LatestCreatedAt, int CountCreatedAfter)> GetRequestHistoryAsync(
        string userId,
        DateTime countCreatedAfter,
        CancellationToken cancellationToken = default
    );

    Task<int> DeleteEntityItemsExpiredBeforeAsync(DateTime expiredBefore, int count, CancellationToken cancellationToken = default);
}
