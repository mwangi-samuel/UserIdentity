using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using UserIdentity.Application.Core.SignInSettings.ViewModels;
using UserIdentity.Domain.AppSignInSettings;
using UserIdentity.Persistence.Repositories.AppSignInSettings;

namespace UserIdentity.Application.Core.SignInSettings.Queries;

public record GetSignInSettingsQuery(int AppId) : IBaseQuery;

public class GetSignInSettingsQueryHandler(IAppSignInSettingsRepository appSignInSettingsRepository)
    : IGetItemQueryHandler<GetSignInSettingsQuery, SignInSettingsViewModel>
{
    private readonly IAppSignInSettingsRepository _appSignInSettingsRepository = appSignInSettingsRepository;

    public async Task<SignInSettingsViewModel> GetItemAsync(GetSignInSettingsQuery query, CancellationToken cancellationToken = default) =>
        new()
        {
            SignInSettings =
                await _appSignInSettingsRepository.GetEntityDtoAsync(query.AppId, cancellationToken)
                ?? new AppSignInSettingsDto { AppId = query.AppId },
        };
}
