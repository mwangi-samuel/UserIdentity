using PolyzenKit.Persistence.Repositories;

using UserIdentity.Domain.Users;

namespace UserIdentity.Persistence.Repositories.Users;

public class UserRepository(
    AppDbContext appDbContext
    ) : EntityRepository<UserEntity, string>(appDbContext), IUserRepository
{
}
