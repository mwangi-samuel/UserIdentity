using Microsoft.EntityFrameworkCore;

using PolyzenKit.Application.Enums;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Common;
using UserIdentity.Domain.InviteCodes;

namespace UserIdentity.Persistence.Repositories.InviteCodes;

public class InviteCodeRepository(
  AppDbContext dbContext
 ) : EntityRepository<InviteCodeEntity, long>(dbContext), IInviteCodeRepository
{
  private readonly AppDbContext _dbContext = dbContext;
  public async Task<InviteCodeEntity?> GetEntityByAlternateIdAsync(InviteCodeEntity entity, QueryCondition queryCondition, CancellationToken cancellationToken = default)
  {
    var userEmail = ObjectUtil.RequireNonNullValue(entity.UserEmail, nameof(entity.UserEmail));

    var existingEntity = await _dbContext.InviteCode
      .Include(e => e.App)
      .SingleOrDefaultAsync(e => e.AppId == entity.AppId && e.UserEmail == userEmail, cancellationToken);

    return queryCondition.Apply(existingEntity, userEmail);
  }

  public async Task<InviteCodeDTO> GetEntityDTOByIdAsync(long id, CancellationToken cancellationToken = default)
  {
    return await _dbContext.InviteCode
      .Where(e => e.Id == id)
      .Select(InviteCodeDTO.Detailed)
      .SingleOrDefaultAsync(cancellationToken) ?? throw new NoRecordException($"{id}", EntityTypes.INVITE_CODE.Description());
  }
}
