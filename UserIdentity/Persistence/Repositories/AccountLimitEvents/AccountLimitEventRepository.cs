using Microsoft.EntityFrameworkCore;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Common.Enums;
using UserIdentity.Domain.AccountLimitEvents;

namespace UserIdentity.Persistence.Repositories.AccountLimitEvents;

public class AccountLimitEventRepository(AppDbContext dbContext)
    : EntityRepository<AccountLimitEventEntity, Guid>(dbContext),
        IAccountLimitEventRepository
{
    public async Task<(int CountCreatedAfter, DateTime? LatestCreatedAt)> GetHistoryAsync(
        string userId,
        AccountLimitKind kind,
        DateTime countCreatedAfter,
        CancellationToken cancellationToken = default
    )
    {
        var history = await DbSet
            .Where(e => e.UserId == userId && e.Kind == kind && e.CreatedAt > countCreatedAfter)
            .GroupBy(e => e.UserId)
            .Select(group => new { CountCreatedAfter = group.Count(), LatestCreatedAt = group.Max(e => e.CreatedAt) })
            .SingleOrDefaultAsync(cancellationToken);

        return history is null ? (0, null) : (history.CountCreatedAfter, history.LatestCreatedAt);
    }

    public async Task<int> DeleteEntityItemsCreatedBeforeAsync(
        DateTime createdBefore,
        int count,
        CancellationToken cancellationToken = default
    ) => await DbSet.Where(e => e.CreatedAt <= createdBefore).OrderBy(e => e.CreatedAt).Take(count).ExecuteDeleteAsync(cancellationToken);
}
