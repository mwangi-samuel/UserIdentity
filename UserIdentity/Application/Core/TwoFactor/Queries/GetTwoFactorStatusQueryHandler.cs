using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Settings;
using PolyzenKit.Domain.Entities.RegisteredApps;
using UserIdentity.Application.Core.SignInSettings.Queries;
using UserIdentity.Application.Core.SignInSettings.ViewModels;
using UserIdentity.Application.Core.TwoFactor.Shared;
using UserIdentity.Application.Core.TwoFactor.ViewModels;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.TwoFactor.Queries;

public record GetTwoFactorStatusQuery(string UserId, RegisteredAppDto RegisteredApp) : IBaseQuery;

public class GetTwoFactorStatusQueryHandler(
    IOptions<RoleSettings> roleSettingsOptions,
    UserManager<IdentityUser> userManager,
    IUserRepository userRepository,
    IGetItemQueryHandler<GetSignInSettingsQuery, SignInSettingsViewModel> getSignInSettingsQueryHandler
) : IGetItemQueryHandler<GetTwoFactorStatusQuery, TwoFactorStatusViewModel>
{
    private readonly RoleSettings _roleSettings = roleSettingsOptions.Value;
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IGetItemQueryHandler<GetSignInSettingsQuery, SignInSettingsViewModel> _getSignInSettingsQueryHandler =
        getSignInSettingsQueryHandler;

    public async Task<TwoFactorStatusViewModel> GetItemAsync(GetTwoFactorStatusQuery query, CancellationToken cancellationToken = default)
    {
        var appName = query.RegisteredApp.AppName;

        var user = await _userManager.FindByIdAsync(query.UserId) ?? throw NoRecordException.ForIdentifier(query.UserId);

        var userDto =
            await _userRepository.GetUserDtoAsync(user.Id, appName, cancellationToken)
            ?? throw NoRecordException.ForIdentifier(query.UserId);

        var signInSettings = (
            await _getSignInSettingsQueryHandler.GetItemAsync(new GetSignInSettingsQuery(query.RegisteredApp.Id), cancellationToken)
        ).SignInSettings;

        var method = AllowedTwoFactorMethods.InUse(signInSettings, userDto.TwoFactorMethod);

        return new TwoFactorStatusViewModel
        {
            Method = method,
            RecoveryCodesLeft = method is null ? 0 : await _userManager.CountRecoveryCodesAsync(user),
            Required = TwoFactorRequirement.IsRequired(signInSettings, await _userManager.GetRolesAsync(user), appName, _roleSettings),
            AllowedMethods = AllowedTwoFactorMethods.For(signInSettings),
        };
    }
}
