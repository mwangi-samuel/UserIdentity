using Microsoft.EntityFrameworkCore;

using PolyzenKit.Application.Enums;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Common;
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

    return queryCondition switch
    {
      QueryCondition.MUST_EXIST when existingEntity is null => throw new NoRecordException($"User Id: {userId} & Forgot Password Token: {forgotPasswordToken}", EntityTypes.USER.Description()),
      QueryCondition.MUST_NOT_EXIST when existingEntity is not null => throw new RecordExistsException($"User Id: {userId} & Forgot Password Token: {forgotPasswordToken}", EntityTypes.USER.Description()),
      _ => existingEntity
    };
  }

}
