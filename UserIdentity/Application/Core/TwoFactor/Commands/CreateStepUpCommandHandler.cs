using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Domain.Entities;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Application.Core.TwoFactor.Queries;
using UserIdentity.Application.Core.TwoFactor.Shared;
using UserIdentity.Application.Core.TwoFactor.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Interfaces;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.StepUps;
using UserIdentity.Persistence.Repositories.StepUps;

namespace UserIdentity.Application.Core.TwoFactor.Commands;

public record CreateStepUpCommand : IBaseCommand
{
    [JsonIgnore]
    public string UserId { get; internal set; } = string.Empty;

    [JsonIgnore]
    public RegisteredAppDto RegisteredApp { get; internal set; } = null!;

    [Required]
    public string Code { get; init; } = null!;
}

public class CreateStepUpCommandHandler(
    IOptions<TwoFactorSettings> twoFactorSettingsOptions,
    UserManager<IdentityUser> userManager,
    IUnitOfWork unitOfWork,
    IStepUpRepository stepUpRepository,
    ITokenFactory tokenFactory,
    ICodeCheckService codeCheckService,
    IGetItemQueryHandler<GetStepUpUserQuery, StepUpUserViewModel> getStepUpUserQueryHandler,
    ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel> recordFailedSignInCommandHandler,
    TimeProvider timeProvider
) : ICreateItemCommandHandler<CreateStepUpCommand, StepUpViewModel>
{
    private readonly TwoFactorSettings _twoFactorSettings = twoFactorSettingsOptions.Value;
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IStepUpRepository _stepUpRepository = stepUpRepository;
    private readonly ITokenFactory _tokenFactory = tokenFactory;
    private readonly ICodeCheckService _codeCheckService = codeCheckService;
    private readonly IGetItemQueryHandler<GetStepUpUserQuery, StepUpUserViewModel> _getStepUpUserQueryHandler = getStepUpUserQueryHandler;
    private readonly ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel> _recordFailedSignInCommandHandler =
        recordFailedSignInCommandHandler;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<StepUpViewModel> CreateItemAsync(
        CreateStepUpCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var stepUpUser = await _getStepUpUserQueryHandler.GetItemAsync(
            new GetStepUpUserQuery(command.UserId, command.RegisteredApp),
            cancellationToken
        );

        var user = stepUpUser.User;
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var pendingStepUp =
            stepUpUser.Method == TwoFactorMethod.Authenticator
                ? null
                : await _stepUpRepository.GetPendingEntityAsync(user.Id, command.RegisteredApp.Id, now, cancellationToken);

        var outcome = await CheckCodeAsync(command, user, stepUpUser.Method, pendingStepUp, cancellationToken);

        if (outcome == CodeCheckOutcome.WrongCode)
        {
            await _recordFailedSignInCommandHandler.CreateItemAsync(new RecordFailedSignInCommand(user, true), user.Id, cancellationToken);
            throw new InvalidDataException(ErrorMessages.Codes.NotAccepted);
        }

        CodeOutcomes.EnsureVerified(outcome);

        var ticket = _tokenFactory.GenerateToken();
        var stepUp =
            pendingStepUp
            ?? new StepUpEntity
            {
                Id = Guid.CreateVersion7(),
                UserId = user.Id,
                AppId = command.RegisteredApp.Id,
            };

        stepUp.Ticket = _tokenFactory.HashToken(ticket);
        stepUp.Expires = now.AddSeconds(_twoFactorSettings.StepUpTicketLifetimeSeconds);

        if (pendingStepUp is null)
        {
            stepUp.SetEntityAuditFields(user.Id, now);
            _stepUpRepository.CreateEntityItem(stepUp);
        }
        else
        {
            stepUp.UpdateEntityAuditFields(user.Id, now);
            _stepUpRepository.UpdateEntityItem(stepUp);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new StepUpViewModel { Ticket = ticket, ExpiresAt = stepUp.Expires };
    }

    private async Task<CodeCheckOutcome> CheckCodeAsync(
        CreateStepUpCommand command,
        IdentityUser user,
        TwoFactorMethod method,
        StepUpEntity? pendingStepUp,
        CancellationToken cancellationToken
    )
    {
        if (method == TwoFactorMethod.Authenticator)
            return await AuthenticatorCodes.IsValidAsync(_userManager, user, command.Code)
                ? CodeCheckOutcome.Verified
                : CodeCheckOutcome.WrongCode;

        var outcome = await _codeCheckService.CheckAsync(
            command.RegisteredApp.AppName,
            UserMessages.LoginCodeAction,
            (pendingStepUp?.Id ?? Guid.CreateVersion7()).ToString(),
            command.Code,
            cancellationToken
        );

        return pendingStepUp is null ? CodeCheckOutcome.NotFound : outcome;
    }
}
