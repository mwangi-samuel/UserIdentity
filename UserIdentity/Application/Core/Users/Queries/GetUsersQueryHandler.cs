using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Paging;
using PolyzenKit.Common.Settings;
using PolyzenKit.Domain.Entities.RegisteredApps;
using UserIdentity.Application.Core.Roles.Shared;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using UserIdentity.Persistence.Repositories.Roles;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.Users.Queries;

public record GetUsersQuery : IBaseQuery
{
    public required RegisteredAppDto RegisteredApp { get; init; }

    public required UserChanger Changer { get; init; }

    public string? Search { get; init; }

    public string? Role { get; init; }

    public bool? EmailConfirmed { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = PagingLimits.DefaultPageSize;
}

public class GetUsersQueryHandler(
    IOptions<RoleSettings> roleSettingsOptions,
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IRefreshTokenRepository refreshTokenRepository,
    TimeProvider timeProvider
) : IGetItemsQueryHandler<GetUsersQuery, UsersViewModel>
{
    private readonly RoleSettings _roleSettings = roleSettingsOptions.Value;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IRoleRepository _roleRepository = roleRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<UsersViewModel> GetItemsAsync(GetUsersQuery query, CancellationToken cancellationToken = default)
    {
        var appName = query.RegisteredApp.AppName;
        var role = string.IsNullOrWhiteSpace(query.Role) ? null : query.Role.Trim();

        if (role is not null && !AppRoleNames.IsAppRole(role, appName))
            throw new InvalidDataException(ErrorMessages.Roles.NameOutsideApp(AppRoleNames.Prefix(appName)));

        var page = await _userRepository.SearchUserDtosByAppIdAsync(
            query.RegisteredApp.Id,
            string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim(),
            role,
            query.EmailConfirmed,
            query.Page,
            query.PageSize,
            cancellationToken
        );

        if (page.Items.Count == 0)
            return new UsersViewModel { Users = [], TotalCount = page.TotalCount };

        List<string> userIds = [.. page.Items.Select(user => user.Id)];

        var rolesByUserId = await _roleRepository.GetRoleNamesByUserIdAsync(userIds, cancellationToken);
        var lastActiveAtByUserId = await _refreshTokenRepository.GetLastActiveAtByUserIdAsync(
            query.RegisteredApp.Id,
            userIds,
            cancellationToken
        );
        var now = _timeProvider.GetUtcNow();

        return new UsersViewModel
        {
            Users =
            [
                .. page.Items.Select(user =>
                    ToListItem(
                        user,
                        rolesByUserId.GetValueOrDefault(user.Id) ?? [],
                        lastActiveAtByUserId.GetValueOrDefault(user.Id),
                        query,
                        now
                    )
                ),
            ],
            TotalCount = page.TotalCount,
        };
    }

    private UserListItemDto ToListItem(UserDto user, List<string> roles, DateTime? lastActiveAt, GetUsersQuery query, DateTimeOffset now)
    {
        var appName = query.RegisteredApp.AppName;

        return new UserListItemDto
        {
            Id = user.Id,
            UserName = user.UserName,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumber = user.PhoneNumber,
            TwoFactorMethod = user.TwoFactorMethod,
            CreatedAt = user.CreatedAt,
            Roles = AppRoleNames.ForApp(roles, appName, _roleSettings.GlobalAdminRole),
            LastActiveAt = lastActiveAt,
            LockedOut = user.LockoutEnd > now,
            DeleteEnabled =
                !query.Changer.IsUser(user.Id) && UserChangeAccess.IsAllowed(query.Changer, user.Id, roles, appName, _roleSettings),
        };
    }
}
