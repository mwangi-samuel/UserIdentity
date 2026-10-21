using Microsoft.EntityFrameworkCore;

using PolyzenKit.Persistence.Repositories;

using UserIdentity.Domain.Users;

namespace UserIdentity.Persistence.Repositories.Users;

public class UserRepository(
    AppDbContext appDbContext
    ) : EntityRepository<UserEntity, string>(appDbContext), IUserRepository
{
  private readonly AppDbContext _appDbContext = appDbContext;

  public async Task<List<UserEntity>> GetUnconfirmedEntityItemsCreatedBeforeAsync(DateTime createdBefore, int count, CancellationToken cancellationToken = default)
  {
    return await _appDbContext.AppUser
      .Where(e => e.CreatedAt < createdBefore)
      .Join(_appDbContext.Users.Where(u => !u.EmailConfirmed && !u.PhoneNumberConfirmed), e => e.Id, u => u.Id, (e, _) => e)
      .OrderBy(e => e.CreatedAt)
      .Take(count)
      .ToListAsync(cancellationToken);
  }
}
