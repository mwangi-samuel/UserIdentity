using System.Security.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Common.Settings;
using UserIdentity.Application.Core.AccountLimits.Queries;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Application.Core.SignInSettings.Queries;
using UserIdentity.Application.Core.SignInSettings.ViewModels;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.TwoFactor.Shared;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Common.Enums;
using UserIdentity.Domain.AppSignInSettings;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.Users.Commands;

public record SignInUserCommand(IdentityUser User, ActiveRegisteredApp RegisteredApp, RequestContext RequestContext) : IBaseCommand
{
    public bool EmailProven { get; init; }

    public bool SecondFactorPassed { get; init; }

    public bool ChangingMethod { get; init; }
}

public class SignInUserCommandHandler(
    IOptions<RoleSettings> roleSettingsOptions,
    UserManager<IdentityUser> userManager,
    IUserRepository userRepository,
    IGetItemQueryHandler<GetSignInSettingsQuery, SignInSettingsViewModel> getSignInSettingsQueryHandler,
    IGetItemQueryHandler<GetAccountLimitQuery, AccountLimitViewModel> getAccountLimitQueryHandler,
    ICreateItemCommandHandler<SendLoginCodeCommand, LoginCodeViewModel> sendLoginCodeCommandHandler,
    ICreateItemCommandHandler<StartSessionCommand, UserTokenViewModel> startSessionCommandHandler,
    ICreateItemCommandHandler<CreateLoginTicketCommand, LoginTicketViewModel> createLoginTicketCommandHandler
) : ICreateItemCommandHandler<SignInUserCommand, AuthUserViewModel>
{
    private readonly RoleSettings _roleSettings = roleSettingsOptions.Value;
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IGetItemQueryHandler<GetSignInSettingsQuery, SignInSettingsViewModel> _getSignInSettingsQueryHandler =
        getSignInSettingsQueryHandler;
    private readonly IGetItemQueryHandler<GetAccountLimitQuery, AccountLimitViewModel> _getAccountLimitQueryHandler =
        getAccountLimitQueryHandler;
    private readonly ICreateItemCommandHandler<SendLoginCodeCommand, LoginCodeViewModel> _sendLoginCodeCommandHandler =
        sendLoginCodeCommandHandler;
    private readonly ICreateItemCommandHandler<StartSessionCommand, UserTokenViewModel> _startSessionCommandHandler =
        startSessionCommandHandler;
    private readonly ICreateItemCommandHandler<CreateLoginTicketCommand, LoginTicketViewModel> _createLoginTicketCommandHandler =
        createLoginTicketCommandHandler;

    public async Task<AuthUserViewModel> CreateItemAsync(
        SignInUserCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var user = command.User;
        var registeredApp = command.RegisteredApp;

        if (command.EmailProven && !user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            IdentityResults.EnsureSucceeded(await _userManager.UpdateAsync(user));
        }

        var userDto =
            await _userRepository.GetUserDtoAsync(user.Id, registeredApp.AppName, cancellationToken)
            ?? throw new InvalidCredentialException($"User {user.Id} has no profile for app {registeredApp.AppName}");

        var signInSettings = (
            await _getSignInSettingsQueryHandler.GetItemAsync(new GetSignInSettingsQuery(registeredApp.Id), cancellationToken)
        ).SignInSettings;

        var method = AllowedTwoFactorMethods.InUse(signInSettings, userDto.TwoFactorMethod);

        if (command.ChangingMethod)
            return command.EmailProven
                ? await SetUpSecondFactorAsync(command, userDto, signInSettings, cancellationToken)
                : await ConfirmEmailAsync(command, userDto, cancellationToken);

        if (!AccountConfirmation.IsConfirmed(signInSettings, user))
            return await ConfirmEmailAsync(command, userDto, cancellationToken);

        if (method is not null && command.SecondFactorPassed)
            return await StartSessionAsync(command, userDto, cancellationToken);

        if (method == TwoFactorMethod.Email && command.EmailProven)
            return await StartSessionAsync(command, userDto, cancellationToken);

        if (method is { } userMethod)
            return await SecondFactorAsync(command, userDto, userMethod, cancellationToken);

        if (TwoFactorRequirement.IsRequired(signInSettings, await _userManager.GetRolesAsync(user), registeredApp.AppName, _roleSettings))
            return command.EmailProven
                ? await SetUpSecondFactorAsync(command, userDto, signInSettings, cancellationToken)
                : await ConfirmEmailAsync(command, userDto, cancellationToken);

        return await StartSessionAsync(command, userDto, cancellationToken);
    }

    private async Task<AuthUserViewModel> StartSessionAsync(SignInUserCommand command, UserDto userDto, CancellationToken cancellationToken)
    {
        var user = command.User;

        IdentityResults.EnsureSucceeded(await _userManager.ResetAccessFailedCountAsync(user));

        var session = await _startSessionCommandHandler.CreateItemAsync(
            new StartSessionCommand(user.Id, user.UserName!, command.RegisteredApp.AppName),
            user.Id,
            cancellationToken
        );

        return new AuthUserViewModel
        {
            User = userDto,
            NextStep = SignInStep.Session,
            UserToken = session.UserToken,
        };
    }

    private async Task<AuthUserViewModel> ConfirmEmailAsync(SignInUserCommand command, UserDto userDto, CancellationToken cancellationToken)
    {
        var user = command.User;
        var ticketId = Guid.CreateVersion7();

        var loginTicket = await _createLoginTicketCommandHandler.CreateItemAsync(
            new CreateLoginTicketCommand
            {
                Id = ticketId,
                UserId = user.Id,
                AppId = command.RegisteredApp.Id,
                Step = SignInStep.ConfirmEmail,
                CodeAction = UserMessages.LoginCodeAction,
                CodeCorrelationId = ticketId.ToString(),
                ChangingMethod = command.ChangingMethod,
            },
            user.Id,
            cancellationToken
        );

        var loginCode = await SendLoginCodeAsync(
            command,
            userDto,
            LoginCodePurpose.ConfirmEmail,
            ticketId,
            loginTicket.Expires,
            user.Email,
            string.IsNullOrWhiteSpace(user.Email) ? user.PhoneNumber : null,
            cancellationToken
        );

        return new AuthUserViewModel
        {
            User = userDto,
            NextStep = SignInStep.ConfirmEmail,
            Ticket = loginTicket.Ticket,
            CodeSentTo = loginCode.CodeSentTo,
        };
    }

    private async Task<AuthUserViewModel> SecondFactorAsync(
        SignInUserCommand command,
        UserDto userDto,
        TwoFactorMethod method,
        CancellationToken cancellationToken
    )
    {
        var user = command.User;

        var failedSecondSteps = await _getAccountLimitQueryHandler.GetItemAsync(
            new GetAccountLimitQuery(user.Id, AccountLimitKind.FailedSecondStep),
            cancellationToken
        );

        if (failedSecondSteps.IsReached)
            throw new InvalidCredentialException($"User {user.Id} reached the daily limit of failed second steps");

        var ticketId = Guid.CreateVersion7();
        var sendsCode = method is TwoFactorMethod.Email or TwoFactorMethod.Sms;

        var loginTicket = await _createLoginTicketCommandHandler.CreateItemAsync(
            new CreateLoginTicketCommand
            {
                Id = ticketId,
                UserId = user.Id,
                AppId = command.RegisteredApp.Id,
                Step = SignInStep.SecondFactor,
                CodeAction = sendsCode ? UserMessages.LoginCodeAction : null,
                CodeCorrelationId = sendsCode ? ticketId.ToString() : null,
                EmailProven = command.EmailProven,
            },
            user.Id,
            cancellationToken
        );

        var loginCode = sendsCode
            ? await SendLoginCodeAsync(
                command,
                userDto,
                LoginCodePurpose.SignIn,
                ticketId,
                loginTicket.Expires,
                method == TwoFactorMethod.Email ? user.Email : null,
                method == TwoFactorMethod.Sms ? user.PhoneNumber : null,
                cancellationToken
            )
            : null;

        return new AuthUserViewModel
        {
            User = userDto,
            NextStep = SignInStep.SecondFactor,
            Ticket = loginTicket.Ticket,
            Method = method,
            CodeSentTo = loginCode?.CodeSentTo,
        };
    }

    private async Task<AuthUserViewModel> SetUpSecondFactorAsync(
        SignInUserCommand command,
        UserDto userDto,
        AppSignInSettingsDto signInSettings,
        CancellationToken cancellationToken
    )
    {
        var loginTicket = await _createLoginTicketCommandHandler.CreateItemAsync(
            new CreateLoginTicketCommand
            {
                UserId = command.User.Id,
                AppId = command.RegisteredApp.Id,
                Step = SignInStep.SetUpSecondFactor,
                EmailProven = true,
            },
            command.User.Id,
            cancellationToken
        );

        return new AuthUserViewModel
        {
            User = userDto,
            NextStep = SignInStep.SetUpSecondFactor,
            Ticket = loginTicket.Ticket,
            AllowedMethods = AllowedTwoFactorMethods.For(signInSettings),
        };
    }

    private async Task<LoginCodeViewModel> SendLoginCodeAsync(
        SignInUserCommand command,
        UserDto userDto,
        LoginCodePurpose purpose,
        Guid ticketId,
        DateTime expiresAt,
        string? email,
        string? phoneNumber,
        CancellationToken cancellationToken
    )
    {
        var loginCode = await _sendLoginCodeCommandHandler.CreateItemAsync(
            new SendLoginCodeCommand
            {
                UserId = command.User.Id,
                UserDetails = userDto,
                AppName = command.RegisteredApp.AppName,
                Purpose = purpose,
                CorrelationId = ticketId.ToString(),
                ExpiresAt = expiresAt,
                Email = email,
                PhoneNumber = phoneNumber,
                RequestContext = command.RequestContext,
            },
            command.User.Id,
            cancellationToken
        );

        return loginCode.Sent
            ? loginCode
            : throw new InvalidCredentialException($"User {command.User.Id} reached the daily limit of login codes");
    }
}
