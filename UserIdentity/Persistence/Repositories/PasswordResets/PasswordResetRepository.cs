using Microsoft.EntityFrameworkCore;

using PolyzenKit.Common.Extensions;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Domain.PasswordResets;

namespace UserIdentity.Persistence.Repositories.PasswordResets;

public class PasswordResetRepository(
  AppDbContext dbContext
 ) : EntityRepository<PasswordResetEntity, Guid>(dbContext), IPasswordResetRepository
{
  private readonly AppDbContext _dbContext = dbContext;

  public async Task<PasswordResetEntity?> GetEntityByAlternateIdAsync(PasswordResetEntity entity, QueryCondition queryCondition, CancellationToken cancellationToken = default)
  {
    var requestId = ObjectExtensions.RequireNonNullValue(entity.RequestId, nameof(entity.RequestId));

    var existingEntity = await _dbContext.PasswordReset
      .SingleOrDefaultAsync(e => e.RequestId == requestId, cancellationToken);

    return queryCondition.Apply(existingEntity, requestId);
  }

  public async Task<PasswordResetEntity?> GetLatestEntityItemAsync(string userId, CancellationToken cancellationToken = default)
  {
    return await _dbContext.PasswordReset
      .Where(e => e.UserId == userId)
      .OrderByDescending(e => e.CreatedAt)
      .FirstOrDefaultAsync(cancellationToken);
  }

  public async Task<int> CountEntityItemsCreatedAfterAsync(string userId, DateTime createdAfter, CancellationToken cancellationToken = default)
  {
    return await _dbContext.PasswordReset
      .CountAsync(e => e.UserId == userId && e.CreatedAt > createdAfter, cancellationToken);
  }

  public async Task<int> DeleteEntityItemsExpiredBeforeAsync(DateTime expiredBefore, int count, CancellationToken cancellationToken = default)
  {
    return await _dbContext.PasswordReset
      .Where(e => e.Expires <= expiredBefore)
      .OrderBy(e => e.Expires)
      .Take(count)
      .ExecuteDeleteAsync(cancellationToken);
  }
}
