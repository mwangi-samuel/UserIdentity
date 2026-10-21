using PolyzenKit.Persistence.Repositories;

using UserIdentity.Domain.PasswordResets;

namespace UserIdentity.Persistence.Repositories.PasswordResets;

public interface IPasswordResetRepository
  : IEntityRepository<PasswordResetEntity, Guid>,
  IItemEntityRepository<PasswordResetEntity, Guid>
{
  Task<PasswordResetEntity?> GetLatestEntityItemAsync(string userId, CancellationToken cancellationToken = default);

  Task<int> CountEntityItemsCreatedAfterAsync(string userId, DateTime createdAfter, CancellationToken cancellationToken = default);

  Task RemoveExpiredEntityItemsAsync(string userId, DateTime now, CancellationToken cancellationToken = default);

  Task RemoveEntityItemsAsync(string userId, CancellationToken cancellationToken = default);

  Task<List<PasswordResetEntity>> GetEntityItemsExpiredBeforeAsync(DateTime now, int count, CancellationToken cancellationToken = default);
}
