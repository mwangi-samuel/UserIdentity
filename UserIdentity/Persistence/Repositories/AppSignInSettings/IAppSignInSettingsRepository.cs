using PolyzenKit.Persistence.Repositories;
using UserIdentity.Domain.AppSignInSettings;

namespace UserIdentity.Persistence.Repositories.AppSignInSettings;

public interface IAppSignInSettingsRepository
    : IEntityRepository<AppSignInSettingsEntity, Guid>,
        IItemEntityRepository<AppSignInSettingsEntity, Guid>
{
    Task<AppSignInSettingsDto?> GetEntityDtoAsync(int appId, CancellationToken cancellationToken = default);
}
