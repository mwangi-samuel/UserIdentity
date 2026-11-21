using System.Security.Authentication;
using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.AccountLimits.Queries;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Application.Core.Tokens.Shared;
using UserIdentity.Application.Core.TwoFactor.ViewModels;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.LoginTickets;
using UserIdentity.Persistence.Repositories.LoginTickets;

namespace UserIdentity.Application.Core.TwoFactor.Queries;

public record GetSecondStepTicketQuery(string Ticket, ActiveRegisteredApp RegisteredApp, SignInStep Step) : IBaseQuery;

public class GetSecondStepTicketQueryHandler(
    ITokenFactory tokenFactory,
    UserManager<IdentityUser> userManager,
    ILoginTicketRepository loginTicketRepository,
    IGetItemQueryHandler<GetAccountLimitQuery, AccountLimitViewModel> getAccountLimitQueryHandler,
    TimeProvider timeProvider
) : IGetItemQueryHandler<GetSecondStepTicketQuery, SecondStepTicketViewModel>
{
    private readonly ITokenFactory _tokenFactory = tokenFactory;
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly ILoginTicketRepository _loginTicketRepository = loginTicketRepository;
    private readonly IGetItemQueryHandler<GetAccountLimitQuery, AccountLimitViewModel> _getAccountLimitQueryHandler =
        getAccountLimitQueryHandler;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<SecondStepTicketViewModel> GetItemAsync(GetSecondStepTicketQuery query, CancellationToken cancellationToken = default)
    {
        var loginTicket = await _loginTicketRepository.GetEntityByAlternateIdAsync(
            new LoginTicketEntity { Ticket = _tokenFactory.HashToken(query.Ticket), AppId = query.RegisteredApp.Id },
            QueryCondition.MayOrMayNotExist,
            cancellationToken
        );

        var user =
            (
                LoginTicketValidity.IsUsable(loginTicket, query.Step, _timeProvider.GetUtcNow().UtcDateTime)
                    ? await _userManager.FindByIdAsync(loginTicket.UserId)
                    : null
            ) ?? throw new InvalidDataException(ErrorMessages.Codes.NotAccepted);
        if (await _userManager.IsLockedOutAsync(user))
            throw new InvalidCredentialException($"User {user.Id} is locked out");

        var failedSecondSteps = await _getAccountLimitQueryHandler.GetItemAsync(
            new GetAccountLimitQuery(user.Id, AccountLimitKind.FailedSecondStep),
            cancellationToken
        );

        if (failedSecondSteps.IsReached)
            throw new InvalidCredentialException($"User {user.Id} reached the daily limit of failed second steps");

        return new SecondStepTicketViewModel { LoginTicket = loginTicket!, User = user };
    }
}
