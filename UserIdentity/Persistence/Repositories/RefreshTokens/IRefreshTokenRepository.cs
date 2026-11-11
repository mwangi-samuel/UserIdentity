using PolyzenKit.Persistence.Repositories;
using UserIdentity.Domain.RefreshTokens;

namespace UserIdentity.Persistence.Repositories.RefreshTokens;

public interface IRefreshTokenRepository : IEntityRepository<RefreshTokenEntity, Guid>, IItemEntityRepository<RefreshTokenEntity, Guid>
{
    Task<(RefreshTokenEntity Entity, bool IsUserRegisteredForApp)?> GetEntityForAppAsync(
        string userId,
        string token,
        string appName,
        CancellationToken cancellationToken = default
    );

    Task<RefreshTokenEntity?> GetEntityByPreviousTokenAsync(
        string userId,
        string previousToken,
        CancellationToken cancellationToken = default
    );

    Task DeleteEntityItemsAsync(string userId, CancellationToken cancellationToken = default);

    Task<int> DeleteEntityItemsExpiredBeforeAsync(DateTime expiredBefore, int count, CancellationToken cancellationToken = default);
}
