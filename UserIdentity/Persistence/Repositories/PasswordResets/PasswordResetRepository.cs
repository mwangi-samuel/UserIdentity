using Microsoft.EntityFrameworkCore;
using PolyzenKit.Common.Extensions;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Domain.PasswordResets;

namespace UserIdentity.Persistence.Repositories.PasswordResets;

public class PasswordResetRepository(AppDbContext dbContext)
    : EntityRepository<PasswordResetEntity, Guid>(dbContext),
        IPasswordResetRepository
{
    public async Task<PasswordResetEntity?> GetEntityByAlternateIdAsync(
        PasswordResetEntity entity,
        QueryCondition queryCondition,
        CancellationToken cancellationToken = default
    )
    {
        var requestId = ObjectExtensions.RequireNonNullValue(entity.RequestId, nameof(entity.RequestId));

        var existingEntity = await DbSet.SingleOrDefaultAsync(e => e.RequestId == requestId, cancellationToken);

        return queryCondition.Apply(existingEntity, requestId);
    }

    public async Task<(DateTime? LatestCreatedAt, int CountCreatedAfter)> GetRequestHistoryAsync(
        string userId,
        DateTime countCreatedAfter,
        CancellationToken cancellationToken = default
    )
    {
        var history = await DbSet
            .Where(e => e.UserId == userId)
            .GroupBy(e => e.UserId)
            .Select(group => new
            {
                LatestCreatedAt = group.Max(e => e.CreatedAt),
                CountCreatedAfter = group.Count(e => e.CreatedAt > countCreatedAfter),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return history is null ? (null, 0) : (history.LatestCreatedAt, history.CountCreatedAfter);
    }

    public async Task<int> DeleteEntityItemsExpiredBeforeAsync(
        DateTime expiredBefore,
        int count,
        CancellationToken cancellationToken = default
    ) => await DbSet.Where(e => e.Expires <= expiredBefore).OrderBy(e => e.Expires).Take(count).ExecuteDeleteAsync(cancellationToken);
}
