using Microsoft.EntityFrameworkCore;
using PolyzenKit.Common.Extensions;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Domain.EmailChanges;

namespace UserIdentity.Persistence.Repositories.EmailChanges;

public class EmailChangeRepository(AppDbContext dbContext) : EntityRepository<EmailChangeEntity, Guid>(dbContext), IEmailChangeRepository
{
    public async Task<EmailChangeEntity?> GetEntityByAlternateIdAsync(
        EmailChangeEntity entity,
        QueryCondition queryCondition,
        CancellationToken cancellationToken = default
    )
    {
        var userId = ObjectExtensions.RequireNonNullValue(entity.UserId, nameof(entity.UserId));

        var existingEntity = await DbSet.SingleOrDefaultAsync(e => e.UserId == userId, cancellationToken);

        return queryCondition.Apply(existingEntity, userId);
    }

    public async Task DeleteEntityItemsAsync(string userId, CancellationToken cancellationToken = default) =>
        await DbSet.Where(e => e.UserId == userId).ExecuteDeleteAsync(cancellationToken);

    public async Task<int> DeleteEntityItemsExpiredBeforeAsync(
        DateTime expiredBefore,
        int count,
        CancellationToken cancellationToken = default
    ) => await DbSet.Where(e => e.Expires <= expiredBefore).OrderBy(e => e.Expires).Take(count).ExecuteDeleteAsync(cancellationToken);
}
