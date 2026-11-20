using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using UserIdentity.Application.Core.AccountLimits.Queries;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Common.Enums;
using UserIdentity.Domain.AccountLimitEvents;
using UserIdentity.Persistence.Repositories.AccountLimitEvents;

namespace UserIdentity.Application.Core.AccountLimits.Commands;

public record CreateAccountLimitEventCommand(string UserId, AccountLimitKind Kind) : IBaseCommand;

public class CreateAccountLimitEventCommandHandler(
    IAccountLimitEventRepository accountLimitEventRepository,
    IGetItemQueryHandler<GetAccountLimitQuery, AccountLimitViewModel> getAccountLimitQueryHandler,
    TimeProvider timeProvider
) : ICreateItemCommandHandler<CreateAccountLimitEventCommand, AccountLimitViewModel>
{
    private readonly IAccountLimitEventRepository _accountLimitEventRepository = accountLimitEventRepository;
    private readonly IGetItemQueryHandler<GetAccountLimitQuery, AccountLimitViewModel> _getAccountLimitQueryHandler =
        getAccountLimitQueryHandler;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<AccountLimitViewModel> CreateItemAsync(
        CreateAccountLimitEventCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var limit = await _getAccountLimitQueryHandler.GetItemAsync(
            new GetAccountLimitQuery(command.UserId, command.Kind),
            cancellationToken
        );

        if (limit.IsReached)
            return limit;

        _accountLimitEventRepository.CreateEntityItem(
            new AccountLimitEventEntity
            {
                UserId = command.UserId,
                Kind = command.Kind,
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
            }
        );

        return limit;
    }
}
