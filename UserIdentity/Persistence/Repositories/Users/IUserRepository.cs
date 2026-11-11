using PolyzenKit.Persistence.Repositories;
using UserIdentity.Domain.Users;

namespace UserIdentity.Persistence.Repositories.Users;

public interface IUserRepository : IEntityRepository<UserEntity, string>
{
    Task<UserDto?> GetUserDtoAsync(string userId, string? appName, CancellationToken cancellationToken = default);

    Task<UserEntity?> GetEntityItemForUpdateAsync(string userId, CancellationToken cancellationToken = default);

    void ClearTrackedEntities();

    Task<List<UserEntity>> GetUnconfirmedEntityItemsCreatedBeforeAsync(
        DateTime createdBefore,
        int count,
        CancellationToken cancellationToken = default
    );
}
