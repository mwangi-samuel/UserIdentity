using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Domain.Entities;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.TwoFactor.Queries;
using UserIdentity.Application.Core.TwoFactor.Shared;
using UserIdentity.Application.Core.TwoFactor.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.StepUps;
using UserIdentity.Persistence.Repositories.StepUps;

namespace UserIdentity.Application.Core.TwoFactor.Commands;

public record SendStepUpCodeCommand(string UserId, RegisteredAppDto RegisteredApp, RequestContext RequestContext) : IBaseCommand;

public class SendStepUpCodeCommandHandler(
    IOptions<TwoFactorSettings> twoFactorSettingsOptions,
    IUnitOfWork unitOfWork,
    IStepUpRepository stepUpRepository,
    IGetItemQueryHandler<GetStepUpUserQuery, StepUpUserViewModel> getStepUpUserQueryHandler,
    ICreateItemCommandHandler<SendLoginCodeCommand, LoginCodeViewModel> sendLoginCodeCommandHandler,
    TimeProvider timeProvider
) : ICreateItemCommandHandler<SendStepUpCodeCommand, StepUpCodeViewModel>
{
    private readonly TwoFactorSettings _twoFactorSettings = twoFactorSettingsOptions.Value;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IStepUpRepository _stepUpRepository = stepUpRepository;
    private readonly IGetItemQueryHandler<GetStepUpUserQuery, StepUpUserViewModel> _getStepUpUserQueryHandler = getStepUpUserQueryHandler;
    private readonly ICreateItemCommandHandler<SendLoginCodeCommand, LoginCodeViewModel> _sendLoginCodeCommandHandler =
        sendLoginCodeCommandHandler;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<StepUpCodeViewModel> CreateItemAsync(
        SendStepUpCodeCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var stepUpUser = await _getStepUpUserQueryHandler.GetItemAsync(
            new GetStepUpUserQuery(command.UserId, command.RegisteredApp),
            cancellationToken
        );

        var method = stepUpUser.Method;

        if (method == TwoFactorMethod.Authenticator)
            return new StepUpCodeViewModel { Method = method };

        var user = stepUpUser.User;
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var stepUp = new StepUpEntity
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            AppId = command.RegisteredApp.Id,
            Expires = now.AddSeconds(_twoFactorSettings.TicketLifetimeSeconds),
        };

        stepUp.SetEntityAuditFields(user.Id, now);

        _stepUpRepository.CreateEntityItem(stepUp);

        var loginCode = await _sendLoginCodeCommandHandler.CreateItemAsync(
            new SendLoginCodeCommand
            {
                UserId = user.Id,
                UserDetails = stepUpUser.UserDetails,
                AppName = command.RegisteredApp.AppName,
                Purpose = LoginCodePurpose.ConfirmAction,
                CorrelationId = stepUp.Id.ToString(),
                ExpiresAt = stepUp.Expires,
                Email = method == TwoFactorMethod.Email ? user.Email : null,
                PhoneNumber = method == TwoFactorMethod.Sms ? user.PhoneNumber : null,
                RequestContext = command.RequestContext,
            },
            user.Id,
            cancellationToken
        );

        if (!loginCode.Sent)
            throw new IllegalOperationException(ErrorMessages.Users.TooManyCodes);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new StepUpCodeViewModel { Method = method, CodeSentTo = loginCode.CodeSentTo };
    }
}
