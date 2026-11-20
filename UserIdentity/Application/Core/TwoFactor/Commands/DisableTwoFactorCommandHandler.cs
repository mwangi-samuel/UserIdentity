using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Commands;
using PolyzenKit.Application.Core.Messages.ViewModels;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Domain.Entities;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Application.Core.TwoFactor.Queries;
using UserIdentity.Application.Core.TwoFactor.Shared;
using UserIdentity.Application.Core.TwoFactor.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.TwoFactor.Commands;

public record DisableTwoFactorCommand : IBaseCommand
{
    [JsonIgnore]
    public string UserId { get; internal set; } = string.Empty;

    [JsonIgnore]
    public ActiveRegisteredApp RegisteredApp { get; internal set; } = null!;

    [JsonIgnore]
    public RequestContext RequestContext { get; internal set; } = new();

    [Required]
    public string Password { get; init; } = null!;
}

public class DisableTwoFactorCommandHandler(
    UserManager<IdentityUser> userManager,
    IUnitOfWork unitOfWork,
    IUserRepository userRepository,
    IGetItemQueryHandler<GetTwoFactorStatusQuery, TwoFactorStatusViewModel> getTwoFactorStatusQueryHandler,
    ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel> recordFailedSignInCommandHandler,
    ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> createOutboxMessageCommandHandler,
    TimeProvider timeProvider
) : IUpdateItemCommandHandler<DisableTwoFactorCommand, TwoFactorStatusViewModel>
{
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IGetItemQueryHandler<GetTwoFactorStatusQuery, TwoFactorStatusViewModel> _getTwoFactorStatusQueryHandler =
        getTwoFactorStatusQueryHandler;
    private readonly ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel> _recordFailedSignInCommandHandler =
        recordFailedSignInCommandHandler;
    private readonly ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> _createOutboxMessageCommandHandler =
        createOutboxMessageCommandHandler;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<TwoFactorStatusViewModel> UpdateItemAsync(
        DisableTwoFactorCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var user = await _userManager.FindByIdAsync(command.UserId) ?? throw NoRecordException.ForIdentifier(command.UserId);

        await PasswordCheck.EnsureAsync(_userManager, _recordFailedSignInCommandHandler, user, command.Password, cancellationToken);

        var status = await _getTwoFactorStatusQueryHandler.GetItemAsync(
            new GetTwoFactorStatusQuery(user.Id, command.RegisteredApp),
            cancellationToken
        );

        if (status.Required)
            throw new IllegalOperationException(ErrorMessages.TwoFactor.Required);

        var userDetails = await _userRepository.GetEntityItemAsync(user.Id, cancellationToken);

        if (userDetails.TwoFactorMethod is not { } method)
            throw new IllegalOperationException(ErrorMessages.TwoFactor.NotOn);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            await TwoFactorCredentials.RemoveAsync(_userManager, user);

            userDetails.TwoFactorMethod = null;
            userDetails.UpdateEntityAuditFields(user.Id, now);

            await _createOutboxMessageCommandHandler.CreateItemAsync(
                new CreateOutboxMessageCommand(
                    UserMessages.TwoFactorChanged,
                    UserMessages.TwoFactorChange(
                        user,
                        userDetails,
                        command.RegisteredApp.AppName,
                        TwoFactorChange.TurnedOff,
                        method,
                        command.RequestContext,
                        now
                    )
                ),
                user.Id,
                cancellationToken
            );

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }

        return status with
        {
            Method = null,
            RecoveryCodesLeft = 0,
        };
    }
}
