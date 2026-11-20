using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using UserIdentity.Application.Core.AccountLimits.Shared;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Common.Enums;
using UserIdentity.Domain.AccountLimitEvents;
using UserIdentity.Persistence.Repositories.AccountLimitEvents;

namespace UserIdentity.Application.Core.AccountLimits.Queries;

public record GetAccountLimitQuery(string UserId, AccountLimitKind Kind) : IBaseQuery;

public class GetAccountLimitQueryHandler(
    IOptions<AccountLimitSettings> accountLimitSettingsOptions,
    IAccountLimitEventRepository accountLimitEventRepository,
    TimeProvider timeProvider
) : IGetItemQueryHandler<GetAccountLimitQuery, AccountLimitViewModel>
{
    private readonly AccountLimitSettings _accountLimitSettings = accountLimitSettingsOptions.Value;
    private readonly IAccountLimitEventRepository _accountLimitEventRepository = accountLimitEventRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<AccountLimitViewModel> GetItemAsync(GetAccountLimitQuery query, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        (var countInWindow, var latestCreatedAt) = await _accountLimitEventRepository.GetHistoryAsync(
            query.UserId,
            query.Kind,
            now - AccountLimitEventLimits.CountWindow,
            cancellationToken
        );

        var isReached = query.Kind switch
        {
            AccountLimitKind.FailedSecondStep => countInWindow >= _accountLimitSettings.MaxFailedSecondStepsPerDay,
            AccountLimitKind.LoginCodeSent => countInWindow >= _accountLimitSettings.MaxLoginCodesPerDay,
            _ => countInWindow >= _accountLimitSettings.MaxSignUpAttemptNoticesPerDay
                || latestCreatedAt > now.AddSeconds(-_accountLimitSettings.SignUpAttemptNoticeCooldownSeconds),
        };

        return new AccountLimitViewModel { IsReached = isReached };
    }
}
