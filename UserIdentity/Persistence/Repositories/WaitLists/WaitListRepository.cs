using Microsoft.EntityFrameworkCore;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Extensions;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Domain.WaitLists;

namespace UserIdentity.Persistence.Repositories.WaitLists;

public class WaitListRepository(AppDbContext dbContext) : EntityRepository<WaitListEntity, long>(dbContext), IWaitListRepository
{
    private readonly AppDbContext _dbContext = dbContext;

    public async Task<WaitListEntity?> GetEntityByAlternateIdAsync(
        WaitListEntity entity,
        QueryCondition queryCondition,
        CancellationToken cancellationToken = default
    )
    {
        var userEmail = ObjectExtensions.RequireNonNullValue(entity.UserEmail, nameof(entity.UserEmail));

        var existingEntity = await DbSet
            .Include(e => e.App)
            .SingleOrDefaultAsync(e => e.AppId == entity.AppId && e.UserEmail == userEmail, cancellationToken);

        return queryCondition.Apply(existingEntity, userEmail);
    }

    public async Task<WaitListDto> GetEntityDtoByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await DbSet.Where(e => e.Id == id).Select(WaitListDto.Detailed).SingleOrDefaultAsync(cancellationToken)
            ?? throw NoRecordException.ForIdentifier($"{id}");
    }

    public async Task<WaitListDto?> GetEntityDtoByEmailAsync(int appId, string userEmail, CancellationToken cancellationToken = default) =>
        await DbSet
            .Where(e => e.AppId == appId && e.UserEmail == userEmail)
            .Select(WaitListDto.Detailed)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<int> DeleteEntityItemsForUserAsync(string userId, CancellationToken cancellationToken = default) =>
        await DbSet
            .Where(e =>
                _dbContext.UserRegisteredApp.Any(registration => registration.UserId == userId && registration.AppId == e.AppId)
                && _dbContext.Users.Any(user => user.Id == userId && user.Email == e.UserEmail)
            )
            .ExecuteDeleteAsync(cancellationToken);
}
