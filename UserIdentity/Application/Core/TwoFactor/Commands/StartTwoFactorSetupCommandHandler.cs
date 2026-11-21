using System.ComponentModel.DataAnnotations;
using System.Security.Authentication;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Domain.Entities;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.SignInSettings.Queries;
using UserIdentity.Application.Core.SignInSettings.ViewModels;
using UserIdentity.Application.Core.TwoFactor.Queries;
using UserIdentity.Application.Core.TwoFactor.Shared;
using UserIdentity.Application.Core.TwoFactor.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.LoginTickets;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.LoginTickets;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.TwoFactor.Commands;

public record StartTwoFactorSetupCommand : IBaseCommand, IValidatableObject
{
    [JsonIgnore]
    public RegisteredAppDto RegisteredApp { get; internal set; } = null!;

    [JsonIgnore]
    public RequestContext RequestContext { get; internal set; } = new();

    [Required]
    public string Ticket { get; init; } = null!;

    [Required]
    public TwoFactorMethod? Method { get; init; }

    [StringLength(UserLimits.PhoneNumberMaxLength)]
    public string? PhoneNumber { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Method == TwoFactorMethod.Sms && string.IsNullOrWhiteSpace(PhoneNumber))
            yield return new ValidationResult(ErrorMessages.TwoFactor.PhoneNumberRequired, [nameof(PhoneNumber)]);
    }
}

public class StartTwoFactorSetupCommandHandler(
    UserManager<IdentityUser> userManager,
    IUnitOfWork unitOfWork,
    IUserRepository userRepository,
    ILoginTicketRepository loginTicketRepository,
    IGetItemQueryHandler<GetSecondStepTicketQuery, SecondStepTicketViewModel> getSecondStepTicketQueryHandler,
    IGetItemQueryHandler<GetSignInSettingsQuery, SignInSettingsViewModel> getSignInSettingsQueryHandler,
    ICreateItemCommandHandler<SendLoginCodeCommand, LoginCodeViewModel> sendLoginCodeCommandHandler,
    TimeProvider timeProvider
) : ICreateItemCommandHandler<StartTwoFactorSetupCommand, TwoFactorSetupViewModel>
{
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly ILoginTicketRepository _loginTicketRepository = loginTicketRepository;
    private readonly IGetItemQueryHandler<GetSecondStepTicketQuery, SecondStepTicketViewModel> _getSecondStepTicketQueryHandler =
        getSecondStepTicketQueryHandler;
    private readonly IGetItemQueryHandler<GetSignInSettingsQuery, SignInSettingsViewModel> _getSignInSettingsQueryHandler =
        getSignInSettingsQueryHandler;
    private readonly ICreateItemCommandHandler<SendLoginCodeCommand, LoginCodeViewModel> _sendLoginCodeCommandHandler =
        sendLoginCodeCommandHandler;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<TwoFactorSetupViewModel> CreateItemAsync(
        StartTwoFactorSetupCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var registeredApp = command.RegisteredApp;
        var method = command.Method!.Value;

        var secondStep = await _getSecondStepTicketQueryHandler.GetItemAsync(
            new GetSecondStepTicketQuery(command.Ticket, registeredApp, SignInStep.SetUpSecondFactor),
            cancellationToken
        );

        var user = secondStep.User;
        var loginTicket = secondStep.LoginTicket;

        var signInSettings = (
            await _getSignInSettingsQueryHandler.GetItemAsync(new GetSignInSettingsQuery(registeredApp.Id), cancellationToken)
        ).SignInSettings;

        if (!AllowedTwoFactorMethods.For(signInSettings).Contains(method))
            throw new InvalidDataException(ErrorMessages.TwoFactor.MethodNotAllowed);

        loginTicket.Method = method;
        loginTicket.PhoneNumber = null;
        loginTicket.CodeAction = null;
        loginTicket.CodeCorrelationId = null;
        loginTicket.UpdateEntityAuditFields(user.Id, _timeProvider.GetUtcNow().UtcDateTime);

        var vm = method switch
        {
            TwoFactorMethod.Authenticator => await StartAuthenticatorAsync(user, registeredApp),
            TwoFactorMethod.Sms => await StartTextMessagesAsync(command, user, loginTicket, cancellationToken),
            _ => new TwoFactorSetupViewModel { Method = method },
        };

        _loginTicketRepository.UpdateEntityItem(loginTicket);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return vm;
    }

    private async Task<TwoFactorSetupViewModel> StartAuthenticatorAsync(IdentityUser user, RegisteredAppDto registeredApp)
    {
        IdentityResults.EnsureSucceeded(await _userManager.ResetAuthenticatorKeyAsync(user));

        var key =
            await _userManager.GetAuthenticatorKeyAsync(user)
            ?? throw new InvalidOperationException($"User {user.Id} has no authenticator key");

        return new TwoFactorSetupViewModel
        {
            Method = TwoFactorMethod.Authenticator,
            AuthenticatorKey = key,
            AuthenticatorUri = AuthenticatorCodes.KeyUri(registeredApp.AppName, user.Email ?? user.UserName!, key),
        };
    }

    private async Task<TwoFactorSetupViewModel> StartTextMessagesAsync(
        StartTwoFactorSetupCommand command,
        IdentityUser user,
        LoginTicketEntity loginTicket,
        CancellationToken cancellationToken
    )
    {
        var phoneNumber = command.PhoneNumber!.Trim();
        var userDto = await _userRepository.GetUserDtoAsync(user.Id, command.RegisteredApp.AppName, cancellationToken) ?? new UserDto();

        var loginCode = await _sendLoginCodeCommandHandler.CreateItemAsync(
            new SendLoginCodeCommand
            {
                UserId = user.Id,
                UserDetails = userDto,
                AppName = command.RegisteredApp.AppName,
                Purpose = LoginCodePurpose.SetUp,
                CorrelationId = loginTicket.Id.ToString(),
                ExpiresAt = loginTicket.Expires,
                PhoneNumber = phoneNumber,
                RequestContext = command.RequestContext,
            },
            user.Id,
            cancellationToken
        );

        if (!loginCode.Sent)
            throw new InvalidCredentialException($"User {user.Id} reached the daily limit of login codes");

        loginTicket.PhoneNumber = phoneNumber;
        loginTicket.CodeAction = UserMessages.LoginCodeAction;
        loginTicket.CodeCorrelationId = loginTicket.Id.ToString();

        return new TwoFactorSetupViewModel { Method = TwoFactorMethod.Sms, CodeSentTo = loginCode.CodeSentTo };
    }
}
