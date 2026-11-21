using Microsoft.EntityFrameworkCore;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Domain.RefreshTokens;

namespace UserIdentity.Persistence.Repositories.RefreshTokens;

public class RefreshTokenRepository(AppDbContext appDbContext)
    : EntityRepository<RefreshTokenEntity, Guid>(appDbContext),
        IRefreshTokenRepository
{
    private readonly AppDbContext _appDbContext = appDbContext;

    public async Task<RefreshTokenEntity?> GetEntityByAlternateIdAsync(
        RefreshTokenEntity entity,
        QueryCondition queryCondition,
        CancellationToken cancellationToken = default
    )
    {
        var existingEntity = await _appDbContext.RefreshToken.SingleOrDefaultAsync(
            e => e.UserId == entity.UserId && e.Token == entity.Token,
            cancellationToken
        );

        return queryCondition.Apply(existingEntity, entity.UserId);
    }

    public async Task<(RefreshTokenEntity Entity, bool IsForApp)?> GetEntityForAppAsync(
        string userId,
        string token,
        string appName,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _appDbContext
            .RefreshToken.Where(e => e.UserId == userId && e.Token == token)
            .Select(e => new
            {
                Entity = e,
                IsForApp = _appDbContext.UserRegisteredApp.Any(registration =>
                    registration.UserId == e.UserId
                    && registration.AppId == e.AppId
                    && registration.App.AppName == appName
                    && !registration.App.IsDeleted
                ),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return result is null ? null : (result.Entity, result.IsForApp);
    }

    public async Task<List<RefreshTokenEntity>> GetEntityItemsAsync(
        string userId,
        int appId,
        DateTime expiresAfter,
        CancellationToken cancellationToken = default
    ) =>
        await _appDbContext
            .RefreshToken.AsNoTracking()
            .Where(e => e.UserId == userId && e.AppId == appId && e.Expires > expiresAfter)
            .OrderByDescending(e => e.UpdatedAt ?? e.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<RefreshTokenEntity?> GetEntityByPreviousTokenAsync(
        string userId,
        string previousToken,
        CancellationToken cancellationToken = default
    ) =>
        await _appDbContext.RefreshToken.SingleOrDefaultAsync(
            e => e.UserId == userId && e.PreviousToken == previousToken,
            cancellationToken
        );

    public async Task<int> DeleteEntityItemsExpiredBeforeAsync(
        DateTime expiredBefore,
        int count,
        CancellationToken cancellationToken = default
    )
    {
        return await _appDbContext
            .RefreshToken.Where(e => e.Expires <= expiredBefore)
            .OrderBy(e => e.Expires)
            .Take(count)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> DeleteEntityItemsAsync(
        string userId,
        int? appId = null,
        Guid? id = null,
        Guid? exceptId = null,
        CancellationToken cancellationToken = default
    ) =>
        await _appDbContext
            .RefreshToken.Where(e =>
                e.UserId == userId
                && (appId == null || e.AppId == appId)
                && (id == null || e.Id == id)
                && (exceptId == null || e.Id != exceptId)
            )
            .ExecuteDeleteAsync(cancellationToken);
}
