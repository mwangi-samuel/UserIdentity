using Microsoft.EntityFrameworkCore;

using PolyzenKit.Application.Enums;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Common;
using UserIdentity.Domain.WaitLists;

namespace UserIdentity.Persistence.Repositories.WaitLists;

public class WaitListRepository(
  AppDbContext dbContext
) : EntityRepository<WaitListEntity, long>(dbContext), IWaitListRepository
{

  private readonly AppDbContext _dbContext = dbContext;

  public async Task<WaitListEntity?> GetEntityByAlternateIdAsync(WaitListEntity entity, QueryCondition queryCondition, CancellationToken cancellationToken = default)
  {
    var userEmail = ObjectUtil.RequireNonNullValue(entity.UserEmail, nameof(entity.UserEmail));

    var existingEntity = await _dbContext.WaitList
      .Include(e => e.App)
      .SingleOrDefaultAsync(e => e.UserEmail == userEmail, cancellationToken);

    return queryCondition switch
    {
      QueryCondition.MUST_EXIST when existingEntity == null => throw new NoRecordException(userEmail, nameof(WaitListEntity)),
      QueryCondition.MUST_NOT_EXIST when existingEntity != null => throw new RecordExistsException(userEmail, nameof(WaitListEntity)),
      _ => existingEntity
    };

  }

  public async Task<WaitListDTO> GetEntityDTOByIdAsync(long id, CancellationToken cancellationToken = default)
  {
    return await _dbContext.WaitList
      .Where(e => e.Id == id && !e.IsDeleted)
      .Select(WaitListDTO.Detailed)
      .SingleOrDefaultAsync(cancellationToken) ?? throw new NoRecordException($"{id}", EntityTypes.WAIT_LIST.Description());
  }
}
