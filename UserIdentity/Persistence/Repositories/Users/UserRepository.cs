using Microsoft.EntityFrameworkCore;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Domain.Users;

namespace UserIdentity.Persistence.Repositories.Users;

public class UserRepository(AppDbContext appDbContext) : EntityRepository<UserEntity, string>(appDbContext), IUserRepository
{
    private readonly AppDbContext _appDbContext = appDbContext;

    public async Task<UserDto?> GetUserDtoAsync(string userId, string? appName, CancellationToken cancellationToken = default)
    {
        return await _appDbContext
            .Users.Where(user => user.Id == userId)
            .Where(user =>
                appName == null
                || _appDbContext.UserRegisteredApp.Any(registration =>
                    registration.UserId == user.Id && registration.App.AppName == appName && !registration.App.IsDeleted
                )
            )
            .Join(
                _appDbContext.AppUser,
                user => user.Id,
                details => details.Id,
                (user, details) =>
                    new UserDto
                    {
                        Id = user.Id,
                        UserName = user.UserName,
                        Email = user.Email,
                        PhoneNumber = user.PhoneNumber,
                        FirstName = details.FirstName,
                        LastName = details.LastName,
                        CreatedBy = details.CreatedBy,
                        CreatedAt = details.CreatedAt,
                        UpdatedBy = details.UpdatedBy,
                        UpdatedAt = details.UpdatedAt,
                    }
            )
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<UserEntity?> GetEntityItemForUpdateAsync(string userId, CancellationToken cancellationToken = default)
    {
        var entities = await _appDbContext
            .AppUser.FromSql($"SELECT * FROM user_details WHERE id = {userId} FOR UPDATE")
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entities.SingleOrDefault();
    }

    public void ClearTrackedEntities() => _appDbContext.ChangeTracker.Clear();

    public async Task<List<UserEntity>> GetUnconfirmedEntityItemsCreatedBeforeAsync(
        DateTime createdBefore,
        int count,
        CancellationToken cancellationToken = default
    )
    {
        return await _appDbContext
            .AppUser.Where(e => e.CreatedAt < createdBefore)
            .Join(_appDbContext.Users.Where(u => !u.EmailConfirmed && !u.PhoneNumberConfirmed), e => e.Id, u => u.Id, (e, _) => e)
            .OrderBy(e => e.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }
}
