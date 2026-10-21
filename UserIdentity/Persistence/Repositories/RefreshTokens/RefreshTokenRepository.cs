using Microsoft.EntityFrameworkCore;

using PolyzenKit.Common.Utilities;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Domain.RefreshTokens;

namespace UserIdentity.Persistence.Repositories.RefreshTokens;

public class RefreshTokenRepository(
    AppDbContext appDbContext
    ) : EntityRepository<RefreshTokenEntity, Guid>(appDbContext), IRefreshTokenRepository
{
  private readonly AppDbContext _appDbContext = appDbContext;

  public async Task<RefreshTokenEntity?> GetEntityByAlternateIdAsync(RefreshTokenEntity entity, QueryCondition queryCondition, CancellationToken cancellationToken = default)
  {
    var userId = ObjectUtil.RequireNonNullValue(entity.UserId, nameof(entity.UserId));
    var token = ObjectUtil.RequireNonNullValue(entity.Token, nameof(entity.Token));

    var existingEntity = await _appDbContext.RefreshToken
        .SingleOrDefaultAsync(e => e.UserId == userId && e.Token == token, cancellationToken);

    return queryCondition.Apply(existingEntity, userId);
  }

  public async Task<RefreshTokenEntity?> GetEntityByPreviousTokenAsync(string userId, string previousToken, CancellationToken cancellationToken = default)
  {
    return await _appDbContext.RefreshToken
        .SingleOrDefaultAsync(e => e.UserId == userId && e.PreviousToken == previousToken, cancellationToken);
  }

  public async Task RemoveExpiredEntityItemsAsync(string userId, DateTime now, CancellationToken cancellationToken = default)
  {
    var expiredEntities = await _appDbContext.RefreshToken
        .Where(e => e.UserId == userId && e.Expires <= now)
        .ToListAsync(cancellationToken);

    _appDbContext.RefreshToken.RemoveRange(expiredEntities);
  }

  public async Task<List<RefreshTokenEntity>> GetEntityItemsExpiredBeforeAsync(DateTime now, int count, CancellationToken cancellationToken = default)
  {
    return await _appDbContext.RefreshToken
        .Where(e => e.Expires <= now)
        .OrderBy(e => e.Expires)
        .Take(count)
        .ToListAsync(cancellationToken);
  }

  public async Task RemoveEntityItemsAsync(string userId, CancellationToken cancellationToken = default)
  {
    var existingEntities = await _appDbContext.RefreshToken
        .Where(e => e.UserId == userId)
        .ToListAsync(cancellationToken);

    _appDbContext.RefreshToken.RemoveRange(existingEntities);
  }
}
