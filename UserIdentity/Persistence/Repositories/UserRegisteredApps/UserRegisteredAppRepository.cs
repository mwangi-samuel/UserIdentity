using Microsoft.EntityFrameworkCore;

using PolyzenKit.Persistence.Repositories;

using UserIdentity.Domain.UserRegisteredApps;

namespace UserIdentity.Persistence.Repositories.UserRegisteredApps;

public class UserRegisteredAppRepository(
    AppDbContext dbContext
 ) : EntityRepository<UserRegisteredAppEntity, string>(dbContext), IUserRegisteredAppRepository
{
  private readonly AppDbContext _dbContext = dbContext;

  public async Task<bool> IsUserRegisteredForAppAsync(string userId, string appName, CancellationToken cancellationToken = default)
  {
    return await _dbContext.UserRegisteredApp
      .AnyAsync(e => e.UserId == userId && e.App.AppName == appName && !e.App.IsDeleted, cancellationToken);
  }
}
