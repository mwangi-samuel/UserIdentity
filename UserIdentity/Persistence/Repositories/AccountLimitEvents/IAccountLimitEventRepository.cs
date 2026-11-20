using PolyzenKit.Persistence.Repositories;
using UserIdentity.Common.Enums;
using UserIdentity.Domain.AccountLimitEvents;

namespace UserIdentity.Persistence.Repositories.AccountLimitEvents;

public interface IAccountLimitEventRepository : IEntityRepository<AccountLimitEventEntity, Guid>
{
    Task<(int CountCreatedAfter, DateTime? LatestCreatedAt)> GetHistoryAsync(
        string userId,
        AccountLimitKind kind,
        DateTime countCreatedAfter,
        CancellationToken cancellationToken = default
    );

    Task<int> DeleteEntityItemsCreatedBeforeAsync(DateTime createdBefore, int count, CancellationToken cancellationToken = default);
}
