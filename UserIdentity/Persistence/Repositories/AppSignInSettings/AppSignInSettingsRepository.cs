using Microsoft.EntityFrameworkCore;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Domain.AppSignInSettings;

namespace UserIdentity.Persistence.Repositories.AppSignInSettings;

public class AppSignInSettingsRepository(AppDbContext dbContext)
    : EntityRepository<AppSignInSettingsEntity, Guid>(dbContext),
        IAppSignInSettingsRepository
{
    public async Task<AppSignInSettingsEntity?> GetEntityByAlternateIdAsync(
        AppSignInSettingsEntity entity,
        QueryCondition queryCondition,
        CancellationToken cancellationToken = default
    )
    {
        var existingEntity = await DbSet.SingleOrDefaultAsync(e => e.AppId == entity.AppId, cancellationToken);

        return queryCondition.Apply(existingEntity, $"{entity.AppId}");
    }

    public async Task<AppSignInSettingsDto?> GetEntityDtoAsync(int appId, CancellationToken cancellationToken = default) =>
        await DbSet.Where(e => e.AppId == appId).Select(AppSignInSettingsDto.Detailed).SingleOrDefaultAsync(cancellationToken);
}
