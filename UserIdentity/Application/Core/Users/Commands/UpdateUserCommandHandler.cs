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
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.Users.Commands;

public record UpdateUserCommand : IBaseCommand
{
    [JsonIgnore]
    public string UserId { get; internal set; } = string.Empty;

    [JsonIgnore]
    public ActiveRegisteredApp RegisteredApp { get; internal set; } = null!;

    [JsonIgnore]
    public UserChanger Changer { get; internal set; } = null!;

    [JsonIgnore]
    public RequestContext RequestContext { get; internal set; } = new();

    [Required, StringLength(UserLimits.FirstNameMaxLength)]
    public string FirstName { get; init; } = null!;

    [StringLength(UserLimits.LastNameMaxLength)]
    public string? LastName { get; init; }

    [StringLength(UserLimits.PhoneNumberMaxLength)]
    public string? PhoneNumber { get; init; }
}

public class UpdateUserCommandHandler(
    IOptions<RoleSettings> roleSettingsOptions,
    UserManager<IdentityUser> userManager,
    IUnitOfWork unitOfWork,
    IUserRepository userRepository,
    IUserRegisteredAppRepository userRegisteredAppRepository,
    ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> createOutboxMessageCommandHandler,
    TimeProvider timeProvider
) : IUpdateItemCommandHandler<UpdateUserCommand, UserViewModel>
{
    private readonly RoleSettings _roleSettings = roleSettingsOptions.Value;
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = userRegisteredAppRepository;
    private readonly ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> _createOutboxMessageCommandHandler =
        createOutboxMessageCommandHandler;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<UserViewModel> UpdateItemAsync(
        UpdateUserCommand command,
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

        var userDetails = await _userRepository.GetEntityItemAsync(user.Id, cancellationToken);
        var phoneNumber = string.IsNullOrWhiteSpace(command.PhoneNumber) ? null : command.PhoneNumber;
        var phoneChanged = !string.Equals(phoneNumber, user.PhoneNumber, StringComparison.Ordinal);

        if (phoneChanged && userDetails.TwoFactorMethod == TwoFactorMethod.Sms)
            throw new IllegalOperationException(ErrorMessages.Users.PhoneUsedForTwoFactor);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            userDetails.FirstName = command.FirstName;
            userDetails.LastName = command.LastName;
            userDetails.UpdateEntityAuditFields(userId, now);

            if (phoneChanged)
            {
                var oldPhoneNumber = user.PhoneNumber;

                IdentityResults.EnsureSucceeded(await _userManager.SetPhoneNumberAsync(user, phoneNumber));

                await _createOutboxMessageCommandHandler.CreateItemAsync(
                    new CreateOutboxMessageCommand(
                        UserMessages.ContactChanged,
                        UserMessages.ContactChange(
                            user.Email,
                            string.IsNullOrWhiteSpace(user.Email) ? oldPhoneNumber : null,
                            userDetails,
                            appName,
                            ContactChange.Phone,
                            command.Changer.NoticeContext(user.Id, command.RequestContext),
                            now
                        )
                    ),
                    userId,
                    cancellationToken
                );
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }

        return new UserViewModel
        {
            User =
                await _userRepository.GetUserDtoAsync(user.Id, appName, cancellationToken)
                ?? throw NoRecordException.ForIdentifier(user.Id),
        };
    }
}
