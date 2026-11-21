using System.ComponentModel.DataAnnotations;
using System.Security.Authentication;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.Tokens.Shared;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Application.Interfaces;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.LoginTickets;
using UserIdentity.Persistence.Repositories.LoginTickets;

namespace UserIdentity.Application.Core.Users.Commands;

public record ConfirmAccountCommand : IBaseCommand
{
    [JsonIgnore]
    public RegisteredAppDto RegisteredApp { get; internal set; } = null!;

    [JsonIgnore]
    public RequestContext RequestContext { get; internal set; } = new();

    [Required]
    public string Ticket { get; init; } = null!;

    [Required]
    public string Code { get; init; } = null!;
}

public class ConfirmAccountCommandHandler(
    ITokenFactory tokenFactory,
    UserManager<IdentityUser> userManager,
    IUnitOfWork unitOfWork,
    ILoginTicketRepository loginTicketRepository,
    ICodeCheckService codeCheckService,
    ICreateItemCommandHandler<SignInUserCommand, AuthUserViewModel> signInUserCommandHandler,
    TimeProvider timeProvider
) : ICreateItemCommandHandler<ConfirmAccountCommand, AuthUserViewModel>
{
    private readonly ITokenFactory _tokenFactory = tokenFactory;
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILoginTicketRepository _loginTicketRepository = loginTicketRepository;
    private readonly ICodeCheckService _codeCheckService = codeCheckService;
    private readonly ICreateItemCommandHandler<SignInUserCommand, AuthUserViewModel> _signInUserCommandHandler = signInUserCommandHandler;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<AuthUserViewModel> CreateItemAsync(
        ConfirmAccountCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var registeredApp = command.RegisteredApp;

        var loginTicket = await _loginTicketRepository.GetEntityByAlternateIdAsync(
            new LoginTicketEntity { Ticket = _tokenFactory.HashToken(command.Ticket), AppId = registeredApp.Id },
            QueryCondition.MayOrMayNotExist,
            cancellationToken
        );

        var user =
            LoginTicketValidity.IsUsable(loginTicket, SignInStep.ConfirmEmail, _timeProvider.GetUtcNow().UtcDateTime)
            && loginTicket.CodeAction is not null
            && loginTicket.CodeCorrelationId is not null
                ? await _userManager.FindByIdAsync(loginTicket.UserId)
                : null;

        if (user is null)
        {
            await _codeCheckService.CheckAsync(
                registeredApp.AppName,
                UserMessages.WelcomeUserAction,
                Guid.CreateVersion7().ToString(),
                command.Code,
                cancellationToken
            );

            throw new InvalidDataException(ErrorMessages.Codes.NotAccepted);
        }

        if (await _userManager.IsLockedOutAsync(user))
            throw new InvalidCredentialException($"User {user.Id} is locked out");

        var outcome = await _codeCheckService.CheckAsync(
            registeredApp.AppName,
            loginTicket!.CodeAction!,
            loginTicket.CodeCorrelationId!,
            command.Code,
            cancellationToken
        );

        CodeOutcomes.EnsureVerified(outcome);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            _loginTicketRepository.RemoveEntityItem(loginTicket);

            var vm = await _signInUserCommandHandler.CreateItemAsync(
                new SignInUserCommand(user, registeredApp, command.RequestContext)
                {
                    EmailProven = true,
                    ChangingMethod = loginTicket.ChangingMethod,
                },
                user.Id,
                cancellationToken
            );

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return vm;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
    }
}
