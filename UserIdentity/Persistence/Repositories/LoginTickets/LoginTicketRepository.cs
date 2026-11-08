using Microsoft.EntityFrameworkCore;
using PolyzenKit.Common.Extensions;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Domain.LoginTickets;

namespace UserIdentity.Persistence.Repositories.LoginTickets;

public class LoginTicketRepository(AppDbContext dbContext) : EntityRepository<LoginTicketEntity, Guid>(dbContext), ILoginTicketRepository
{
    private readonly AppDbContext _dbContext = dbContext;

    public async Task<LoginTicketEntity?> GetEntityByAlternateIdAsync(
        LoginTicketEntity entity,
        QueryCondition queryCondition,
        CancellationToken cancellationToken = default
    )
    {
        var ticket = ObjectExtensions.RequireNonNullValue(entity.Ticket, nameof(entity.Ticket));

        var existingEntity = await _dbContext.LoginTicket.SingleOrDefaultAsync(
            e => e.Ticket == ticket && e.AppId == entity.AppId,
            cancellationToken
        );

        return queryCondition.Apply(existingEntity, ticket);
    }

    public async Task<int> DeleteEntityItemsExpiredBeforeAsync(
        DateTime expiredBefore,
        int count,
        CancellationToken cancellationToken = default
    ) =>
        await _dbContext
            .LoginTicket.Where(e => e.Expires <= expiredBefore)
            .OrderBy(e => e.Expires)
            .Take(count)
            .ExecuteDeleteAsync(cancellationToken);
}
