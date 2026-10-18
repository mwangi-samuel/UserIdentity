using Microsoft.EntityFrameworkCore;

using PolyzenKit.Application.Enums;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Domain.Users;


namespace UserIdentity.Persistence.Repositories.Users;

public class UserRepository(
    AppDbContext appDbContext
    ) : EntityRepository<UserEntity, string>(appDbContext), IUserRepository
{
  private readonly AppDbContext _appDbContext = appDbContext;


  public async Task<UserEntity?> GetEntityByAlternateIdAsync(UserEntity entity, QueryCondition queryCondition, CancellationToken cancellationToken = default)
  {
    var userId = ObjectUtil.RequireNonNullValue(entity.Id, nameof(entity.Id));
    var forgotPasswordToken = ObjectUtil.RequireNonNullValue(entity.ForgotPasswordToken, nameof(entity.ForgotPasswordToken));

    var existingEntity = await _appDbContext.AppUser
        .SingleOrDefaultAsync(e => e.Id == userId && e.ForgotPasswordToken == forgotPasswordToken, cancellationToken);

    return queryCondition.Apply(existingEntity, userId);
  }

}
