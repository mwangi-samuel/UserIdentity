using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Settings;
using PolyzenKit.Domain.Entities.RegisteredApps;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.Users.Queries;

public record GetUserByEmailQuery : IBaseQuery
{
    public required string Email { get; init; }

    public required RegisteredAppDto RegisteredApp { get; init; }

    public required UserChanger Changer { get; init; }
}

public class GetUserByEmailQueryHandler(
    IOptions<RoleSettings> roleSettingsOptions,
    UserManager<IdentityUser> userManager,
    IUserRepository userRepository,
    IUserRegisteredAppRepository userRegisteredAppRepository
) : IGetItemQueryHandler<GetUserByEmailQuery, UserViewModel>
{
    private readonly RoleSettings _roleSettings = roleSettingsOptions.Value;
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = userRegisteredAppRepository;

    public async Task<UserViewModel> GetItemAsync(GetUserByEmailQuery query, CancellationToken cancellationToken = default)
    {
        var appName = query.RegisteredApp.AppName;

        var user = await UserChangeAccess.EnsureAllowedAsync(
            _userManager,
            _userRegisteredAppRepository,
            _roleSettings,
            await _userManager.FindByEmailAsync(query.Email),
            query.Email,
            appName,
            query.Changer,
            cancellationToken
        );

        return new UserViewModel
        {
            User =
                await _userRepository.GetUserDtoAsync(user.Id, appName, cancellationToken)
                ?? throw NoRecordException.ForIdentifier(query.Email),
        };
    }
}
