using Microsoft.EntityFrameworkCore;
using PolyzenKit.Common.Extensions;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Domain.StepUps;

namespace UserIdentity.Persistence.Repositories.StepUps;

public class StepUpRepository(AppDbContext dbContext) : EntityRepository<StepUpEntity, Guid>(dbContext), IStepUpRepository
{
    public async Task<StepUpEntity?> GetEntityByAlternateIdAsync(
        StepUpEntity entity,
        QueryCondition queryCondition,
        CancellationToken cancellationToken = default
    )
    {
        var ticket = ObjectExtensions.RequireNonNullValue(entity.Ticket, nameof(entity.Ticket));

        var existingEntity = await DbSet.SingleOrDefaultAsync(e => e.Ticket == ticket && e.AppId == entity.AppId, cancellationToken);

        return queryCondition.Apply(existingEntity, ticket);
    }

    public async Task<StepUpEntity?> GetPendingEntityAsync(
        string userId,
        int appId,
        DateTime expiresAfter,
        CancellationToken cancellationToken = default
    ) =>
        await DbSet
            .Where(e => e.UserId == userId && e.AppId == appId && e.Ticket == null && e.Expires > expiresAfter)
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<int> DeleteEntityItemsExpiredBeforeAsync(
        DateTime expiredBefore,
        int count,
        CancellationToken cancellationToken = default
    ) => await DbSet.Where(e => e.Expires <= expiredBefore).OrderBy(e => e.Expires).Take(count).ExecuteDeleteAsync(cancellationToken);
}
