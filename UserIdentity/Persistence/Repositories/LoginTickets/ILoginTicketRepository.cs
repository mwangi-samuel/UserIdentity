using PolyzenKit.Persistence.Repositories;
using UserIdentity.Domain.LoginTickets;

namespace UserIdentity.Persistence.Repositories.LoginTickets;

public interface ILoginTicketRepository : IEntityRepository<LoginTicketEntity, Guid>, IItemEntityRepository<LoginTicketEntity, Guid>
{
    Task<int> DeleteEntityItemsExpiredBeforeAsync(DateTime expiredBefore, int count, CancellationToken cancellationToken = default);
}
