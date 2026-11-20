using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
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
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.TwoFactor.Commands;

public record VerifySecondFactorCommand : IBaseCommand, IValidatableObject
{
    [JsonIgnore]
    public ActiveRegisteredApp RegisteredApp { get; internal set; } = null!;

    [JsonIgnore]
    public RequestContext RequestContext { get; internal set; } = new();

    [Required]
    public string Ticket { get; init; } = null!;

    public string? Code { get; init; }

    public string? RecoveryCode { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Code) == string.IsNullOrWhiteSpace(RecoveryCode))
            yield return new ValidationResult(ErrorMessages.TwoFactor.CodeOrRecoveryCodeRequired, [nameof(Code), nameof(RecoveryCode)]);
    }
}

public class VerifySecondFactorCommandHandler(
    UserManager<IdentityUser> userManager,
    IUnitOfWork unitOfWork,
    IUserRepository userRepository,
    ILoginTicketRepository loginTicketRepository,
    ICodeCheckService codeCheckService,
    IGetItemQueryHandler<GetSecondStepTicketQuery, SecondStepTicketViewModel> getSecondStepTicketQueryHandler,
    IGetItemQueryHandler<GetSignInSettingsQuery, SignInSettingsViewModel> getSignInSettingsQueryHandler,
    ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel> recordFailedSignInCommandHandler,
    ICreateItemCommandHandler<SignInUserCommand, AuthUserViewModel> signInUserCommandHandler
) : ICreateItemCommandHandler<VerifySecondFactorCommand, AuthUserViewModel>
{
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly ILoginTicketRepository _loginTicketRepository = loginTicketRepository;
    private readonly ICodeCheckService _codeCheckService = codeCheckService;
    private readonly IGetItemQueryHandler<GetSecondStepTicketQuery, SecondStepTicketViewModel> _getSecondStepTicketQueryHandler =
        getSecondStepTicketQueryHandler;
    private readonly IGetItemQueryHandler<GetSignInSettingsQuery, SignInSettingsViewModel> _getSignInSettingsQueryHandler =
        getSignInSettingsQueryHandler;
    private readonly ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel> _recordFailedSignInCommandHandler =
        recordFailedSignInCommandHandler;
    private readonly ICreateItemCommandHandler<SignInUserCommand, AuthUserViewModel> _signInUserCommandHandler = signInUserCommandHandler;

    public async Task<AuthUserViewModel> CreateItemAsync(
        VerifySecondFactorCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var registeredApp = command.RegisteredApp;

        var secondStep = await _getSecondStepTicketQueryHandler.GetItemAsync(
            new GetSecondStepTicketQuery(command.Ticket, registeredApp, SignInStep.SecondFactor),
            cancellationToken
        );

        var user = secondStep.User;
        var loginTicket = secondStep.LoginTicket;

        var userDto = await _userRepository.GetUserDtoAsync(user.Id, registeredApp.AppName, cancellationToken);

        var signInSettings = (
            await _getSignInSettingsQueryHandler.GetItemAsync(new GetSignInSettingsQuery(registeredApp.Id), cancellationToken)
        ).SignInSettings;

        if (AllowedTwoFactorMethods.InUse(signInSettings, userDto?.TwoFactorMethod) is not { } method)
            throw new InvalidDataException(ErrorMessages.Codes.NotAccepted);

        var outcome = await CheckCodeAsync(command, user, loginTicket, method, cancellationToken);

        if (outcome == CodeCheckOutcome.WrongCode)
        {
            await _recordFailedSignInCommandHandler.CreateItemAsync(new RecordFailedSignInCommand(user, true), user.Id, cancellationToken);
            throw new InvalidDataException(ErrorMessages.Codes.NotAccepted);
        }

        CodeOutcomes.EnsureVerified(outcome);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            _loginTicketRepository.RemoveEntityItem(loginTicket);

            var vm = await _signInUserCommandHandler.CreateItemAsync(
                new SignInUserCommand(user, registeredApp, command.RequestContext)
                {
                    EmailProven = loginTicket.EmailProven || (method == TwoFactorMethod.Email && command.RecoveryCode is null),
                    SecondFactorPassed = true,
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

    private async Task<CodeCheckOutcome> CheckCodeAsync(
        VerifySecondFactorCommand command,
        IdentityUser user,
        LoginTicketEntity loginTicket,
        TwoFactorMethod method,
        CancellationToken cancellationToken
    )
    {
        if (command.RecoveryCode is { } recoveryCode)
            return (await _userManager.RedeemTwoFactorRecoveryCodeAsync(user, recoveryCode.Trim())).Succeeded
                ? CodeCheckOutcome.Verified
                : CodeCheckOutcome.WrongCode;

        if (method == TwoFactorMethod.Authenticator)
            return await AuthenticatorCodes.IsValidAsync(_userManager, user, command.Code!)
                ? CodeCheckOutcome.Verified
                : CodeCheckOutcome.WrongCode;

        if (loginTicket.CodeAction is null || loginTicket.CodeCorrelationId is null)
            return CodeCheckOutcome.NotFound;

        return await _codeCheckService.CheckAsync(
            command.RegisteredApp.AppName,
            loginTicket.CodeAction,
            loginTicket.CodeCorrelationId,
            command.Code!,
            cancellationToken
        );
    }
}
