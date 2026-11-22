using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Domain.Entities.RegisteredApps;
using UserIdentity.Application.Core.AccountLimits.Queries;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Application.Core.SignInSettings.Queries;
using UserIdentity.Application.Core.SignInSettings.ViewModels;
using UserIdentity.Application.Core.TwoFactor.Shared;
using UserIdentity.Application.Core.TwoFactor.ViewModels;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.TwoFactor.Queries;

public record GetStepUpUserQuery(string UserId, RegisteredAppDto RegisteredApp) : IBaseQuery;

public class GetStepUpUserQueryHandler(
    UserManager<IdentityUser> userManager,
    IUserRepository userRepository,
    IGetItemQueryHandler<GetAccountLimitQuery, AccountLimitViewModel> getAccountLimitQueryHandler,
    IGetItemQueryHandler<GetSignInSettingsQuery, SignInSettingsViewModel> getSignInSettingsQueryHandler
) : IGetItemQueryHandler<GetStepUpUserQuery, StepUpUserViewModel>
{
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IGetItemQueryHandler<GetAccountLimitQuery, AccountLimitViewModel> _getAccountLimitQueryHandler =
        getAccountLimitQueryHandler;
    private readonly IGetItemQueryHandler<GetSignInSettingsQuery, SignInSettingsViewModel> _getSignInSettingsQueryHandler =
        getSignInSettingsQueryHandler;

    public async Task<StepUpUserViewModel> GetItemAsync(GetStepUpUserQuery query, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(query.UserId) ?? throw NoRecordException.ForIdentifier(query.UserId);

        if (await _userManager.IsLockedOutAsync(user))
            throw new InvalidDataException(ErrorMessages.Users.AccountLocked);

        var failedSecondSteps = await _getAccountLimitQueryHandler.GetItemAsync(
            new GetAccountLimitQuery(user.Id, AccountLimitKind.FailedSecondStep),
            cancellationToken
        );

        if (failedSecondSteps.IsReached)
            throw new InvalidDataException(ErrorMessages.Users.AccountLocked);

        var userDetails =
            await _userRepository.GetUserDtoAsync(user.Id, query.RegisteredApp.AppName, cancellationToken)
            ?? throw NoRecordException.ForIdentifier(query.UserId);

        var signInSettings = (
            await _getSignInSettingsQueryHandler.GetItemAsync(new GetSignInSettingsQuery(query.RegisteredApp.Id), cancellationToken)
        ).SignInSettings;

        var method =
            AllowedTwoFactorMethods.InUse(signInSettings, userDetails.TwoFactorMethod)
            ?? throw new IllegalOperationException(ErrorMessages.TwoFactor.SetUpToConfirm);

        return new StepUpUserViewModel
        {
            User = user,
            UserDetails = userDetails,
            Method = method,
        };
    }
}
