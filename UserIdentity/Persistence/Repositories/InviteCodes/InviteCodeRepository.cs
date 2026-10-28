using Microsoft.EntityFrameworkCore;

using PolyzenKit.Application.Enums;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Paging;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Persistence.Repositories;
using PolyzenKit.Persistence.Utilities;

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

  public async Task<InviteCodeDto> GetEntityDtoByIdAsync(long id, CancellationToken cancellationToken = default)
  {
    return await _dbContext.InviteCode
      .Where(e => e.Id == id)
      .Select(InviteCodeDto.Detailed)
      .SingleOrDefaultAsync(cancellationToken) ?? throw new NoRecordException($"{id}", EntityTypes.INVITE_CODE.Description());
  }

  public async Task<PagedList<InviteCodeDto>> SearchEntityDtosByAppIdAsync(int appId, string? search, bool? applied, int page, int pageSize, CancellationToken cancellationToken = default)
  {
    return await _dbContext.InviteCode
      .Where(invite => invite.AppId == appId && (search == null || invite.UserEmail.Contains(search)))
      .Where(invite => applied == null || invite.Applied == applied)
      .OrderByDescending(invite => invite.CreatedAt).ThenByDescending(invite => invite.Id)
      .Select(InviteCodeDto.Detailed).ToPageAsync(page, pageSize, cancellationToken);
  }

  public async Task<PagedList<InviteCodeDto>> GetEntityDtosByAppIdAsync(int appId, int page, int pageSize, CancellationToken cancellationToken = default)
  {
    return await _dbContext.InviteCode
      .Where(e => e.AppId == appId)
      .OrderByDescending(e => e.CreatedAt)
      .ThenByDescending(e => e.Id)
      .Select(InviteCodeDto.Detailed)
      .ToPageAsync(page, pageSize, cancellationToken);
  }
}
