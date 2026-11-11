using System.ComponentModel.DataAnnotations;
using System.Security.Authentication;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.LoginTickets;
using UserIdentity.Persistence.Repositories.LoginTickets;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.Tokens.Commands;

public record ExchangeLoginTicketCommand : IBaseCommand
{
    [JsonIgnore]
    public ActiveRegisteredApp RegisteredApp { get; internal set; } = null!;

    [Required]
    public string Ticket { get; init; } = null!;
}

public class ExchangeLoginTicketCommandHandler(
    IOptions<IdentityOptions> identityOptions,
    ITokenFactory tokenFactory,
    UserManager<IdentityUser> userManager,
    IUnitOfWork unitOfWork,
    IUserRepository userRepository,
    ILoginTicketRepository loginTicketRepository,
    ICreateItemCommandHandler<StartSessionCommand, UserTokenViewModel> startSessionCommandHandler,
    TimeProvider timeProvider
) : ICreateItemCommandHandler<ExchangeLoginTicketCommand, AuthUserViewModel>
{
    private readonly IdentityOptions _identityOptions = identityOptions.Value;
    private readonly ITokenFactory _tokenFactory = tokenFactory;
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly ILoginTicketRepository _loginTicketRepository = loginTicketRepository;
    private readonly ICreateItemCommandHandler<StartSessionCommand, UserTokenViewModel> _startSessionCommandHandler =
        startSessionCommandHandler;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<AuthUserViewModel> CreateItemAsync(
        ExchangeLoginTicketCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var registeredApp = command.RegisteredApp;

        var loginTicket =
            await _loginTicketRepository.GetEntityByAlternateIdAsync(
                new LoginTicketEntity { Ticket = _tokenFactory.HashToken(command.Ticket), AppId = registeredApp.Id },
                QueryCondition.MayOrMayNotExist,
                cancellationToken
            ) ?? throw new InvalidCredentialException("Unknown login ticket");

        if (loginTicket.Expires <= _timeProvider.GetUtcNow().UtcDateTime)
            throw new InvalidCredentialException($"Expired login ticket for user {loginTicket.UserId}");

        var user =
            await _userManager.FindByIdAsync(loginTicket.UserId)
            ?? throw new InvalidCredentialException($"Login ticket for missing user {loginTicket.UserId}");

        if (await _userManager.IsLockedOutAsync(user))
            throw new InvalidCredentialException($"User {user.Id} is locked out");

        if (!AccountConfirmation.IsConfirmed(_identityOptions, user))
            throw new IllegalOperationException(ErrorMessages.Users.AccountNotConfirmed);

        var userDto =
            await _userRepository.GetUserDtoAsync(user.Id, registeredApp.AppName, cancellationToken)
            ?? throw new InvalidCredentialException($"User {user.Id} has no profile for app {registeredApp.AppName}");

        _loginTicketRepository.RemoveEntityItem(loginTicket);

        var session = await _startSessionCommandHandler.CreateItemAsync(
            new StartSessionCommand(user.Id, user.UserName!, registeredApp.AppName),
            user.Id,
            cancellationToken
        );

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthUserViewModel
        {
            User = userDto,
            UserToken = session.UserToken,
            IsConfirmed = true,
        };
    }
}
