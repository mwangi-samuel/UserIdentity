using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PolyzenKit.Common.Paging;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Domain.Users;

namespace UserIdentity.Persistence.Repositories.Users;

public class UserRepository(AppDbContext appDbContext) : EntityRepository<UserEntity, string>(appDbContext), IUserRepository
{
    private static readonly Expression<Func<IdentityUser, UserEntity, UserDto>> _userDto = (user, details) =>
        new UserDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumber = user.PhoneNumber,
            LockoutEnd = user.LockoutEnd,
            FirstName = details.FirstName,
            LastName = details.LastName,
            TwoFactorMethod = details.TwoFactorMethod,
            CreatedBy = details.CreatedBy,
            CreatedAt = details.CreatedAt,
            UpdatedBy = details.UpdatedBy,
            UpdatedAt = details.UpdatedAt,
        };

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
            .Join(_appDbContext.AppUser, user => user.Id, details => details.Id, _userDto)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedList<UserDto>> SearchUserDtosByAppIdAsync(
        int appId,
        string? search,
        string? roleName,
        bool? emailConfirmed,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default
    )
    {
        var searchPattern = LikePattern.Containing(search);
        var normalizedRoleName = roleName?.ToUpperInvariant();

        return await _appDbContext
            .Users.Where(user =>
                _appDbContext.UserRegisteredApp.Any(registration => registration.UserId == user.Id && registration.AppId == appId)
            )
            .Where(user => emailConfirmed == null || user.EmailConfirmed == emailConfirmed)
            .Where(user =>
                normalizedRoleName == null
                || _appDbContext.UserRoles.Any(userRole =>
                    userRole.UserId == user.Id
                    && _appDbContext.Roles.Any(role => role.Id == userRole.RoleId && role.NormalizedName == normalizedRoleName)
                )
            )
            .Join(_appDbContext.AppUser, user => user.Id, details => details.Id, _userDto)
            .Where(user =>
                searchPattern == null
                || EF.Functions.ILike(user.Email!, searchPattern, LikePattern.EscapeCharacter)
                || EF.Functions.ILike(user.UserName!, searchPattern, LikePattern.EscapeCharacter)
                || EF.Functions.ILike(user.FirstName!, searchPattern, LikePattern.EscapeCharacter)
                || EF.Functions.ILike(user.LastName!, searchPattern, LikePattern.EscapeCharacter)
                || EF.Functions.ILike(user.PhoneNumber!, searchPattern, LikePattern.EscapeCharacter)
            )
            .OrderByDescending(user => user.CreatedAt)
            .ThenByDescending(user => user.Id)
            .ToPageAsync(page, pageSize, cancellationToken);
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
            .Where(e =>
                !_appDbContext.UserRegisteredApp.Any(link =>
                    link.UserId == e.Id
                    && !_appDbContext.AppSignInSettings.Any(settings => settings.AppId == link.AppId && settings.RequireConfirmedAccount)
                )
            )
            .Join(_appDbContext.Users.Where(u => !u.EmailConfirmed && !u.PhoneNumberConfirmed), e => e.Id, u => u.Id, (e, _) => e)
            .OrderBy(e => e.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }
}
