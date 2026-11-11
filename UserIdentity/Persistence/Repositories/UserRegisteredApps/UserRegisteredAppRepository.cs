using Microsoft.EntityFrameworkCore;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Domain.UserRegisteredApps;

namespace UserIdentity.Persistence.Repositories.UserRegisteredApps;

public class UserRegisteredAppRepository(AppDbContext dbContext)
    : EntityRepository<UserRegisteredAppEntity, Guid>(dbContext),
        IUserRegisteredAppRepository
{
    public async Task<bool> IsUserRegisteredForAppAsync(string userId, string appName, CancellationToken cancellationToken = default) =>
        await DbSet.AnyAsync(e => e.UserId == userId && e.App.AppName == appName && !e.App.IsDeleted, cancellationToken);
}
