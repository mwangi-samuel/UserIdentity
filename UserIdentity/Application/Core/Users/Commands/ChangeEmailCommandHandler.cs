using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Commands;
using PolyzenKit.Application.Core.Messages.ViewModels;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Settings;
using PolyzenKit.Domain.Entities;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.AccountLimits.Commands;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Application.Core.TwoFactor.Shared;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Common.Validation;
using UserIdentity.Domain.EmailChanges;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.EmailChanges;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.Users.Commands;

public record ChangeEmailCommand : IBaseCommand
{
    [JsonIgnore]
    public string UserId { get; internal set; } = string.Empty;

    [JsonIgnore]
    public ActiveRegisteredApp RegisteredApp { get; internal set; } = null!;

    [JsonIgnore]
    public UserChanger Changer { get; internal set; } = null!;

    [JsonIgnore]
    public RequestContext RequestContext { get; internal set; } = new();

    [
        Required,
        StringLength(UserLimits.EmailMaxLength),
        RegularExpression(ValidationPatterns.Email, ErrorMessage = ErrorMessages.Users.InvalidEmail)
    ]
    public string Email { get; init; } = null!;

    public string? Password { get; init; }
}

public class ChangeEmailCommandHandler(
    IOptions<RoleSettings> roleSettingsOptions,
    IOptions<TwoFactorSettings> twoFactorSettingsOptions,
    UserManager<IdentityUser> userManager,
    IUnitOfWork unitOfWork,
    IUserRepository userRepository,
    IUserRegisteredAppRepository userRegisteredAppRepository,
    IEmailChangeRepository emailChangeRepository,
    ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel> recordFailedSignInCommandHandler,
    ICreateItemCommandHandler<CreateAccountLimitEventCommand, AccountLimitViewModel> createAccountLimitEventCommandHandler,
    ICreateItemCommandHandler<SendLoginCodeCommand, LoginCodeViewModel> sendLoginCodeCommandHandler,
    ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> createOutboxMessageCommandHandler,
    TimeProvider timeProvider
) : ICreateItemCommandHandler<ChangeEmailCommand, EmailChangeViewModel>
{
    private readonly RoleSettings _roleSettings = roleSettingsOptions.Value;
    private readonly TwoFactorSettings _twoFactorSettings = twoFactorSettingsOptions.Value;
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = userRegisteredAppRepository;
    private readonly IEmailChangeRepository _emailChangeRepository = emailChangeRepository;
    private readonly ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel> _recordFailedSignInCommandHandler =
        recordFailedSignInCommandHandler;
    private readonly ICreateItemCommandHandler<
        CreateAccountLimitEventCommand,
        AccountLimitViewModel
    > _createAccountLimitEventCommandHandler = createAccountLimitEventCommandHandler;
    private readonly ICreateItemCommandHandler<SendLoginCodeCommand, LoginCodeViewModel> _sendLoginCodeCommandHandler =
        sendLoginCodeCommandHandler;
    private readonly ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> _createOutboxMessageCommandHandler =
        createOutboxMessageCommandHandler;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<EmailChangeViewModel> CreateItemAsync(
        ChangeEmailCommand command,
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

        var newEmail = command.Email.Trim();

        if (command.Changer.IsUser(user.Id))
        {
            if (string.IsNullOrWhiteSpace(command.Password))
                throw new InvalidDataException(ErrorMessages.Users.PasswordRequired);

            await PasswordCheck.EnsureAsync(_userManager, _recordFailedSignInCommandHandler, user, command.Password, cancellationToken);
        }

        if (string.Equals(newEmail, user.Email, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(ErrorMessages.Users.EmailNotChanged);

        var isTaken = await IsTakenAsync(newEmail, user);
        var userDetails = await GetUserDtoAsync(user, appName, cancellationToken);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            await _emailChangeRepository.DeleteEntityItemsAsync(user.Id, cancellationToken);

            var vm = command.Changer.IsUser(user.Id)
                ? await StartOwnChangeAsync(command, user, userDetails, newEmail, isTaken, cancellationToken)
                : await ApplyAdminChangeAsync(command, user, userDetails, newEmail, isTaken, userId, cancellationToken);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return vm;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<EmailChangeViewModel> StartOwnChangeAsync(
        ChangeEmailCommand command,
        IdentityUser user,
        UserDto userDetails,
        string newEmail,
        bool isTaken,
        CancellationToken cancellationToken
    )
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var emailChange = new EmailChangeEntity
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            NewEmail = newEmail,
            Expires = now.AddSeconds(_twoFactorSettings.TicketLifetimeSeconds),
        };

        emailChange.SetEntityAuditFields(user.Id, now);

        _emailChangeRepository.CreateEntityItem(emailChange);

        var codeSent = isTaken
            ? !(
                await _createAccountLimitEventCommandHandler.CreateItemAsync(
                    new CreateAccountLimitEventCommand(user.Id, AccountLimitKind.LoginCodeSent),
                    user.Id,
                    cancellationToken
                )
            ).IsReached
            : (
                await _sendLoginCodeCommandHandler.CreateItemAsync(
                    new SendLoginCodeCommand
                    {
                        UserId = user.Id,
                        UserDetails = userDetails,
                        AppName = command.RegisteredApp.AppName,
                        Purpose = LoginCodePurpose.ChangeEmail,
                        CorrelationId = emailChange.Id.ToString(),
                        ExpiresAt = emailChange.Expires,
                        Email = newEmail,
                        RequestContext = command.RequestContext,
                    },
                    user.Id,
                    cancellationToken
                )
            ).Sent;

        if (!codeSent)
            throw new IllegalOperationException(ErrorMessages.Users.TooManyCodes);

        return new EmailChangeViewModel { User = userDetails, CodeSentTo = ContactMask.Email(newEmail) };
    }

    private async Task<EmailChangeViewModel> ApplyAdminChangeAsync(
        ChangeEmailCommand command,
        IdentityUser user,
        UserDto userDetails,
        string newEmail,
        bool isTaken,
        string userId,
        CancellationToken cancellationToken
    )
    {
        if (isTaken)
            throw new InvalidDataException(ErrorMessages.Users.EmailNotChangedByAdmin);

        var oldEmail = user.Email;

        await UserEmailUpdate.ApplyAsync(_userManager, user, newEmail, false);

        await _createOutboxMessageCommandHandler.CreateItemAsync(
            new CreateOutboxMessageCommand(
                UserMessages.ContactChanged,
                UserMessages.ContactChange(
                    oldEmail,
                    string.IsNullOrWhiteSpace(oldEmail) ? user.PhoneNumber : null,
                    userDetails,
                    command.RegisteredApp.AppName,
                    ContactChange.Email,
                    new RequestContext(),
                    _timeProvider.GetUtcNow().UtcDateTime
                )
            ),
            userId,
            cancellationToken
        );

        return new EmailChangeViewModel { User = await GetUserDtoAsync(user, command.RegisteredApp.AppName, cancellationToken) };
    }

    private async Task<bool> IsTakenAsync(string newEmail, IdentityUser user) =>
        (await _userManager.FindByEmailAsync(newEmail) is { } emailOwner && emailOwner.Id != user.Id)
        || (await _userManager.FindByNameAsync(newEmail) is { } userNameOwner && userNameOwner.Id != user.Id);

    private async Task<UserDto> GetUserDtoAsync(IdentityUser user, string appName, CancellationToken cancellationToken) =>
        await _userRepository.GetUserDtoAsync(user.Id, appName, cancellationToken) ?? throw NoRecordException.ForIdentifier(user.Id);
}
