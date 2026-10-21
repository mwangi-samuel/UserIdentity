using Microsoft.EntityFrameworkCore;

using PolyzenKit.Common.Utilities;
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
    var requestId = ObjectUtil.RequireNonNullValue(entity.RequestId, nameof(entity.RequestId));

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

  public async Task RemoveEntityItemsAsync(string userId, CancellationToken cancellationToken = default)
  {
    var existingEntities = await _dbContext.PasswordReset
      .Where(e => e.UserId == userId)
      .ToListAsync(cancellationToken);

    _dbContext.PasswordReset.RemoveRange(existingEntities);
  }

  public async Task<List<PasswordResetEntity>> GetEntityItemsExpiredBeforeAsync(DateTime now, int count, CancellationToken cancellationToken = default)
  {
    return await _dbContext.PasswordReset
      .Where(e => e.Expires <= now)
      .OrderBy(e => e.Expires)
      .Take(count)
      .ToListAsync(cancellationToken);
  }

  public async Task RemoveExpiredEntityItemsAsync(string userId, DateTime now, CancellationToken cancellationToken = default)
  {
    var expiredEntities = await _dbContext.PasswordReset
      .Where(e => e.UserId == userId && e.Expires <= now)
      .ToListAsync(cancellationToken);

    _dbContext.PasswordReset.RemoveRange(expiredEntities);
  }
}
