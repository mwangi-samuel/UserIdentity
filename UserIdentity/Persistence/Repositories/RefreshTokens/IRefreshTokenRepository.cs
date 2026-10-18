using PolyzenKit.Persistence.Repositories;

using UserIdentity.Domain.RefreshTokens;

namespace UserIdentity.Persistence.Repositories.RefreshTokens;

public interface IRefreshTokenRepository
    : IEntityRepository<RefreshTokenEntity, Guid>,
    IItemEntityRepository<RefreshTokenEntity, Guid>
{
  Task<RefreshTokenEntity?> GetEntityByPreviousTokenAsync(string userId, string previousToken, CancellationToken cancellationToken = default);

  Task RemoveExpiredEntityItemsAsync(string userId, DateTime now, CancellationToken cancellationToken = default);

  Task RemoveEntityItemsAsync(string userId, CancellationToken cancellationToken = default);
}
