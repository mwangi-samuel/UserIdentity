using Microsoft.EntityFrameworkCore;

using PolyzenKit.Application.Enums;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Common;
using UserIdentity.Domain.RefreshTokens;

namespace UserIdentity.Persistence.Repositories.RefreshTokens;

public class RefreshTokenRepository(
    AppDbContext appDbContext
    ) : EntityRepository<RefreshTokenEntity, Guid>(appDbContext), IRefreshTokenRepository
{
  private readonly AppDbContext _appDbContext = appDbContext;

  public async Task<RefreshTokenEntity?> GetEntityByAlternateIdAsync(RefreshTokenEntity entity, QueryCondition queryCondition, CancellationToken cancellationToken = default)
  {
    var userId = ObjectUtil.RequireNonNullValue(entity.UserId, nameof(entity.UserId));
    var token = ObjectUtil.RequireNonNullValue(entity.Token, nameof(entity.Token));

    var existingEntity = await _appDbContext.RefreshToken
        .SingleOrDefaultAsync(e => e.UserId == userId && e.Token == token, cancellationToken);

    return queryCondition switch
    {
      QueryCondition.MUST_EXIST when existingEntity is null => throw new NoRecordException($"User Id: {userId} & Token: {token}", EntityTypes.REFRESH_TOKEN.Description()),
      QueryCondition.MUST_NOT_EXIST when existingEntity is not null => throw new RecordExistsException($"User Id: {userId} & Token: {token}", EntityTypes.REFRESH_TOKEN.Description()),
      _ => existingEntity
    };
  }
}
