using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Commands;
using PolyzenKit.Application.Core.Messages.ViewModels;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Domain.Entities;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Application.Core.SignInSettings.Queries;
using UserIdentity.Application.Core.SignInSettings.ViewModels;
using UserIdentity.Application.Core.TwoFactor.Queries;
using UserIdentity.Application.Core.TwoFactor.Shared;
using UserIdentity.Application.Core.TwoFactor.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Application.Interfaces;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.LoginTickets;
using UserIdentity.Persistence.Repositories.LoginTickets;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.TwoFactor.Commands;

public record ConfirmTwoFactorSetupCommand : IBaseCommand
{
    [JsonIgnore]
    public ActiveRegisteredApp RegisteredApp { get; internal set; } = null!;

    [JsonIgnore]
    public RequestContext RequestContext { get; internal set; } = new();

    [Required]
    public string Ticket { get; init; } = null!;

    public string? Code { get; init; }
}

public class ConfirmTwoFactorSetupCommandHandler(
    IOptions<TwoFactorSettings> twoFactorSettingsOptions,
    UserManager<IdentityUser> userManager,
    IUnitOfWork unitOfWork,
    IUserRepository userRepository,
    ILoginTicketRepository loginTicketRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ICodeCheckService codeCheckService,
    IGetItemQueryHandler<GetSecondStepTicketQuery, SecondStepTicketViewModel> getSecondStepTicketQueryHandler,
    IGetItemQueryHandler<GetSignInSettingsQuery, SignInSettingsViewModel> getSignInSettingsQueryHandler,
    ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel> recordFailedSignInCommandHandler,
    ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> createOutboxMessageCommandHandler,
    ICreateItemCommandHandler<SignInUserCommand, AuthUserViewModel> signInUserCommandHandler,
    TimeProvider timeProvider
) : ICreateItemCommandHandler<ConfirmTwoFactorSetupCommand, AuthUserViewModel>
{
    private readonly TwoFactorSettings _twoFactorSettings = twoFactorSettingsOptions.Value;
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly ILoginTicketRepository _loginTicketRepository = loginTicketRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
    private readonly ICodeCheckService _codeCheckService = codeCheckService;
    private readonly IGetItemQueryHandler<GetSecondStepTicketQuery, SecondStepTicketViewModel> _getSecondStepTicketQueryHandler =
        getSecondStepTicketQueryHandler;
    private readonly IGetItemQueryHandler<GetSignInSettingsQuery, SignInSettingsViewModel> _getSignInSettingsQueryHandler =
        getSignInSettingsQueryHandler;
    private readonly ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel> _recordFailedSignInCommandHandler =
        recordFailedSignInCommandHandler;
    private readonly ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> _createOutboxMessageCommandHandler =
        createOutboxMessageCommandHandler;
    private readonly ICreateItemCommandHandler<SignInUserCommand, AuthUserViewModel> _signInUserCommandHandler = signInUserCommandHandler;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<AuthUserViewModel> CreateItemAsync(
        ConfirmTwoFactorSetupCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var registeredApp = command.RegisteredApp;

        var secondStep = await _getSecondStepTicketQueryHandler.GetItemAsync(
            new GetSecondStepTicketQuery(command.Ticket, registeredApp, SignInStep.SetUpSecondFactor),
            cancellationToken
        );

        var user = secondStep.User;
        var loginTicket = secondStep.LoginTicket;
        var method = loginTicket.Method ?? throw new InvalidDataException(ErrorMessages.TwoFactor.ChooseMethodFirst);

        var signInSettings = (
            await _getSignInSettingsQueryHandler.GetItemAsync(new GetSignInSettingsQuery(registeredApp.Id), cancellationToken)
        ).SignInSettings;

        if (!AllowedTwoFactorMethods.For(signInSettings).Contains(method))
            throw new InvalidDataException(ErrorMessages.TwoFactor.MethodNotAllowed);

        if (method != TwoFactorMethod.Email && string.IsNullOrWhiteSpace(command.Code))
            throw new InvalidDataException(ErrorMessages.TwoFactor.CodeRequired);

        var outcome = method switch
        {
            TwoFactorMethod.Authenticator => await AuthenticatorCodes.IsValidAsync(_userManager, user, command.Code!)
                ? CodeCheckOutcome.Verified
                : CodeCheckOutcome.WrongCode,
            TwoFactorMethod.Sms => await CheckTextMessageCodeAsync(command, loginTicket, cancellationToken),
            _ => CodeCheckOutcome.Verified,
        };

        if (outcome == CodeCheckOutcome.WrongCode)
        {
            await _recordFailedSignInCommandHandler.CreateItemAsync(new RecordFailedSignInCommand(user, true), user.Id, cancellationToken);
            throw new InvalidDataException(ErrorMessages.Codes.NotAccepted);
        }

        CodeOutcomes.EnsureVerified(outcome);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var recoveryCodes = await SaveMethodAsync(command, user, loginTicket, method, cancellationToken);

            await _refreshTokenRepository.DeleteEntityItemsAsync(user.Id, cancellationToken);

            _loginTicketRepository.RemoveEntityItem(loginTicket);

            var vm = await _signInUserCommandHandler.CreateItemAsync(
                new SignInUserCommand(user, registeredApp, command.RequestContext)
                {
                    EmailProven = loginTicket.EmailProven,
                    SecondFactorPassed = true,
                },
                user.Id,
                cancellationToken
            );

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return vm with
            {
                RecoveryCodes = recoveryCodes,
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<CodeCheckOutcome> CheckTextMessageCodeAsync(
        ConfirmTwoFactorSetupCommand command,
        LoginTicketEntity loginTicket,
        CancellationToken cancellationToken
    ) =>
        loginTicket.CodeAction is null || loginTicket.CodeCorrelationId is null || loginTicket.PhoneNumber is null
            ? CodeCheckOutcome.NotFound
            : await _codeCheckService.CheckAsync(
                command.RegisteredApp.AppName,
                loginTicket.CodeAction,
                loginTicket.CodeCorrelationId,
                command.Code!,
                cancellationToken
            );

    private async Task<List<string>> SaveMethodAsync(
        ConfirmTwoFactorSetupCommand command,
        IdentityUser user,
        LoginTicketEntity loginTicket,
        TwoFactorMethod method,
        CancellationToken cancellationToken
    )
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var userDetails = await _userRepository.GetEntityItemAsync(user.Id, cancellationToken);
        var previousMethod = userDetails.TwoFactorMethod;

        if (method == TwoFactorMethod.Sms)
        {
            user.PhoneNumber = loginTicket.PhoneNumber;
            user.PhoneNumberConfirmed = true;
            IdentityResults.EnsureSucceeded(await _userManager.UpdateAsync(user));
        }

        if (method != TwoFactorMethod.Authenticator)
            await TwoFactorCredentials.RemoveAuthenticatorKeyAsync(_userManager, user);

        userDetails.TwoFactorMethod = method;
        userDetails.UpdateEntityAuditFields(user.Id, now);

        IdentityResults.EnsureSucceeded(await _userManager.SetTwoFactorEnabledAsync(user, true));

        var recoveryCodes =
            await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, _twoFactorSettings.RecoveryCodeCount)
            ?? throw new InvalidOperationException($"No recovery codes were made for user {user.Id}");

        await _createOutboxMessageCommandHandler.CreateItemAsync(
            new CreateOutboxMessageCommand(
                UserMessages.TwoFactorChanged,
                UserMessages.TwoFactorChange(
                    user,
                    userDetails,
                    command.RegisteredApp.AppName,
                    previousMethod is null ? TwoFactorChange.SetUp : TwoFactorChange.Changed,
                    method,
                    command.RequestContext,
                    now
                )
            ),
            user.Id,
            cancellationToken
        );

        return [.. recoveryCodes];
    }
}
