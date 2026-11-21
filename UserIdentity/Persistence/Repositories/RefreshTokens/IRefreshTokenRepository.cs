using PolyzenKit.Persistence.Repositories;
using UserIdentity.Domain.RefreshTokens;

namespace UserIdentity.Persistence.Repositories.RefreshTokens;

public interface IRefreshTokenRepository : IEntityRepository<RefreshTokenEntity, Guid>, IItemEntityRepository<RefreshTokenEntity, Guid>
{
    Task<(RefreshTokenEntity Entity, bool IsForApp)?> GetEntityForAppAsync(
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

    Task<List<RefreshTokenEntity>> GetEntityItemsAsync(
        string userId,
        int appId,
        DateTime expiresAfter,
        CancellationToken cancellationToken = default
    );

    Task<int> DeleteEntityItemsAsync(
        string userId,
        int? appId = null,
        Guid? id = null,
        Guid? exceptId = null,
        CancellationToken cancellationToken = default
    );

    Task<int> DeleteEntityItemsExpiredBeforeAsync(DateTime expiredBefore, int count, CancellationToken cancellationToken = default);
}
