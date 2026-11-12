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

    public async Task<(RefreshTokenEntity Entity, bool IsUserRegisteredForApp)?> GetEntityForAppAsync(
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
                IsUserRegisteredForApp = _appDbContext.UserRegisteredApp.Any(registration =>
                    registration.UserId == e.UserId && registration.App.AppName == appName && !registration.App.IsDeleted
                ),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return result is null ? null : (result.Entity, result.IsUserRegisteredForApp);
    }

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

    public async Task DeleteEntityItemsAsync(string userId, CancellationToken cancellationToken = default) =>
        await _appDbContext.RefreshToken.Where(e => e.UserId == userId).ExecuteDeleteAsync(cancellationToken);
}
