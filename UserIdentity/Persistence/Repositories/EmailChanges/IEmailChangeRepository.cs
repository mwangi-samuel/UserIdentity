using PolyzenKit.Persistence.Repositories;
using UserIdentity.Domain.EmailChanges;

namespace UserIdentity.Persistence.Repositories.EmailChanges;

public interface IEmailChangeRepository : IEntityRepository<EmailChangeEntity, Guid>, IItemEntityRepository<EmailChangeEntity, Guid>
{
    Task DeleteEntityItemsAsync(string userId, CancellationToken cancellationToken = default);

    Task<int> DeleteEntityItemsExpiredBeforeAsync(DateTime expiredBefore, int count, CancellationToken cancellationToken = default);
}
