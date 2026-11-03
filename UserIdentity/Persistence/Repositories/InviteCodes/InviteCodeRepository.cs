using Microsoft.EntityFrameworkCore;

using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Extensions;
using PolyzenKit.Common.Paging;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Domain.InviteCodes;

namespace UserIdentity.Persistence.Repositories.InviteCodes;

public class InviteCodeRepository(
  AppDbContext dbContext
 ) : EntityRepository<InviteCodeEntity, long>(dbContext), IInviteCodeRepository
{
  private readonly AppDbContext _dbContext = dbContext;
  public async Task<InviteCodeEntity?> GetEntityByAlternateIdAsync(
    InviteCodeEntity entity,
    QueryCondition queryCondition,
    CancellationToken cancellationToken = default)
  {
    var userEmail = ObjectExtensions.RequireNonNullValue(entity.UserEmail, nameof(entity.UserEmail));

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
      .SingleOrDefaultAsync(cancellationToken) ?? throw NoRecordException.ForIdentifier($"{id}");
  }

  public async Task<List<InviteCodeEntity>> GetAppliedEntityItemsForUserAsync(
    string userId,
    CancellationToken cancellationToken = default)
  {
    return await _dbContext.InviteCode
      .Where(invite => invite.Applied
        && _dbContext.UserRegisteredApp.Any(registration => registration.UserId == userId
          && registration.AppId == invite.AppId)
        && _dbContext.Users.Any(user => user.Id == userId && user.Email == invite.UserEmail))
      .ToListAsync(cancellationToken);
  }

  public async Task<PagedList<InviteCodeDto>> SearchEntityDtosByAppIdAsync(
    int appId,
    string? search,
    bool? applied,
    int page,
    int pageSize,
    CancellationToken cancellationToken = default)
  {
    return await _dbContext.InviteCode
      .Where(invite => invite.AppId == appId && (search == null || invite.UserEmail.Contains(search)))
      .Where(invite => applied == null || invite.Applied == applied)
      .OrderByDescending(invite => invite.CreatedAt).ThenByDescending(invite => invite.Id)
      .Select(InviteCodeDto.Detailed).ToPageAsync(page, pageSize, cancellationToken);
  }
}
