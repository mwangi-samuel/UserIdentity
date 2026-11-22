using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Commands;
using PolyzenKit.Application.Core.Messages.ViewModels;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Extensions;
using PolyzenKit.Common.Settings;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.StepUps;
using UserIdentity.Persistence.Repositories.InviteCodes;
using UserIdentity.Persistence.Repositories.StepUps;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.Persistence.Repositories.Users;
using UserIdentity.Persistence.Repositories.WaitLists;

namespace UserIdentity.Application.Core.Users.Commands;

public record DeleteUserCommand : IBaseCommand
{
    [JsonIgnore]
    public string UserId { get; internal set; } = string.Empty;

    [JsonIgnore]
    public RegisteredAppDto RegisteredApp { get; internal set; } = null!;

    [JsonIgnore]
    public UserChanger Changer { get; internal set; } = null!;

    [JsonIgnore]
    public int ChangerAppId { get; internal set; }

    [JsonIgnore]
    public RequestContext RequestContext { get; internal set; } = new();

    public string? Password { get; init; }

    public string? Ticket { get; init; }
}

public class DeleteUserCommandHandler(
    IOptions<RoleSettings> roleSettingsOptions,
    UserManager<IdentityUser> userManager,
    IUnitOfWork unitOfWork,
    IUserRepository userRepository,
    IUserRegisteredAppRepository userRegisteredAppRepository,
    IInviteCodeRepository inviteCodeRepository,
    IWaitListRepository waitListRepository,
    IStepUpRepository stepUpRepository,
    ITokenFactory tokenFactory,
    ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel> recordFailedSignInCommandHandler,
    ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> createOutboxMessageCommandHandler,
    TimeProvider timeProvider
) : IDeleteItemCommandHandler<DeleteUserCommand, DeleteRecordViewModel>
{
    private readonly RoleSettings _roleSettings = roleSettingsOptions.Value;
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = userRegisteredAppRepository;
    private readonly IInviteCodeRepository _inviteCodeRepository = inviteCodeRepository;
    private readonly IWaitListRepository _waitListRepository = waitListRepository;
    private readonly IStepUpRepository _stepUpRepository = stepUpRepository;
    private readonly ITokenFactory _tokenFactory = tokenFactory;
    private readonly ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel> _recordFailedSignInCommandHandler =
        recordFailedSignInCommandHandler;
    private readonly ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> _createOutboxMessageCommandHandler =
        createOutboxMessageCommandHandler;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<DeleteRecordViewModel> DeleteItemAsync(
        DeleteUserCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var appName = command.RegisteredApp.AppName;

        var user = await UserChangeAccess.EnsureAllowedAsync(
            _userManager,
            _userRegisteredAppRepository,
            _roleSettings,
            await _userManager.FindByIdAsync(command.UserId),
            command.UserId,
            appName,
            command.Changer,
            cancellationToken
        );

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (command.Changer.IsUser(user.Id))
        {
            if (string.IsNullOrWhiteSpace(command.Password))
                throw new InvalidDataException(ErrorMessages.Users.PasswordRequiredToDeleteAccount);

            await PasswordCheck.EnsureAsync(_userManager, _recordFailedSignInCommandHandler, user, command.Password, cancellationToken);
        }
        else
            await EnsureStepUpAsync(command, now, cancellationToken);

        var globalAdminRole = _roleSettings.GlobalAdminRole.TrimAndLowerInvariant();

        if (await _userManager.IsInRoleAsync(user, globalAdminRole) && (await _userManager.GetUsersInRoleAsync(globalAdminRole)).Count == 1)
            throw new IllegalOperationException(ErrorMessages.Users.LastGlobalAdmin);

        var userDetails = await _userRepository.GetEntityItemAsync(user.Id, cancellationToken);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            await _inviteCodeRepository.DeleteEntityItemsForUserAsync(user.Id, cancellationToken);
            await _waitListRepository.DeleteEntityItemsForUserAsync(user.Id, cancellationToken);

            await _createOutboxMessageCommandHandler.CreateItemAsync(
                new CreateOutboxMessageCommand(
                    UserMessages.UserDeleted,
                    UserMessages.AccountDeletion(
                        user,
                        userDetails,
                        appName,
                        command.Changer.NoticeContext(user.Id, command.RequestContext),
                        now
                    )
                ),
                userId,
                cancellationToken
            );

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            IdentityResults.EnsureSucceeded(await _userManager.DeleteAsync(user));

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }

        return new DeleteRecordViewModel();
    }

    private async Task EnsureStepUpAsync(DeleteUserCommand command, DateTime now, CancellationToken cancellationToken)
    {
        var stepUp = string.IsNullOrWhiteSpace(command.Ticket)
            ? null
            : await _stepUpRepository.GetEntityByAlternateIdAsync(
                new StepUpEntity { Ticket = _tokenFactory.HashToken(command.Ticket), AppId = command.ChangerAppId },
                QueryCondition.MayOrMayNotExist,
                cancellationToken
            );

        if (stepUp is null || stepUp.UserId != command.Changer.UserId || stepUp.Expires <= now)
            throw new IllegalOperationException(ErrorMessages.TwoFactor.ConfirmFirst);
    }
}
