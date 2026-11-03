using Microsoft.EntityFrameworkCore;

using PolyzenKit.Common.Extensions;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Domain.RefreshTokens;

namespace UserIdentity.Persistence.Repositories.RefreshTokens;

public class RefreshTokenRepository(
    AppDbContext appDbContext
    ) : EntityRepository<RefreshTokenEntity, Guid>(appDbContext), IRefreshTokenRepository
{
  private readonly AppDbContext _appDbContext = appDbContext;

  public async Task<RefreshTokenEntity?> GetEntityByAlternateIdAsync(
    RefreshTokenEntity entity,
    QueryCondition queryCondition,
    CancellationToken cancellationToken = default)
  {
    var userId = ObjectExtensions.RequireNonNullValue(entity.UserId, nameof(entity.UserId));
    var token = ObjectExtensions.RequireNonNullValue(entity.Token, nameof(entity.Token));

    var existingEntity = await _appDbContext.RefreshToken
        .SingleOrDefaultAsync(e => e.UserId == userId && e.Token == token, cancellationToken);

    return queryCondition.Apply(existingEntity, userId);
  }

  public async Task<RefreshTokenEntity?> GetEntityByPreviousTokenAsync(
    string userId,
    string previousToken,
    CancellationToken cancellationToken = default)
  {
    return await _appDbContext.RefreshToken
        .SingleOrDefaultAsync(e => e.UserId == userId && e.PreviousToken == previousToken, cancellationToken);
  }

  public async Task<int> DeleteEntityItemsExpiredBeforeAsync(
    DateTime expiredBefore,
    int count,
    CancellationToken cancellationToken = default)
  {
    return await _appDbContext.RefreshToken
        .Where(e => e.Expires <= expiredBefore)
        .OrderBy(e => e.Expires)
        .Take(count)
        .ExecuteDeleteAsync(cancellationToken);
  }

  public async Task RemoveEntityItemsAsync(string userId, CancellationToken cancellationToken = default)
  {
    var existingEntities = await _appDbContext.RefreshToken
        .Where(e => e.UserId == userId)
        .ToListAsync(cancellationToken);

    _appDbContext.RefreshToken.RemoveRange(existingEntities);
  }
}
