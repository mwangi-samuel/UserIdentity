using PolyzenKit.Persistence.Repositories;

using UserIdentity.Domain.Users;

namespace UserIdentity.Persistence.Repositories.Users;

public interface IUserRepository : IEntityRepository<UserEntity, string>
{
  Task<List<UserEntity>> GetUnconfirmedEntityItemsCreatedBeforeAsync(DateTime createdBefore, int count, CancellationToken cancellationToken = default);
}
