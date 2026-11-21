using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Commands;
using PolyzenKit.Application.Core.Messages.ViewModels;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Settings;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Application.Interfaces;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.EmailChanges;
using UserIdentity.Persistence.Repositories.EmailChanges;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.Users.Commands;

public record ConfirmEmailChangeCommand : IBaseCommand
{
    [JsonIgnore]
    public string UserId { get; internal set; } = string.Empty;

    [JsonIgnore]
    public RegisteredAppDto RegisteredApp { get; internal set; } = null!;

    [JsonIgnore]
    public UserChanger Changer { get; internal set; } = null!;

    [JsonIgnore]
    public RequestContext RequestContext { get; internal set; } = new();

    [Required]
    public string Code { get; init; } = null!;
}

public class ConfirmEmailChangeCommandHandler(
    IOptions<RoleSettings> roleSettingsOptions,
    UserManager<IdentityUser> userManager,
    IUnitOfWork unitOfWork,
    IUserRepository userRepository,
    IUserRegisteredAppRepository userRegisteredAppRepository,
    IEmailChangeRepository emailChangeRepository,
    ICodeCheckService codeCheckService,
    ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> createOutboxMessageCommandHandler,
    TimeProvider timeProvider
) : ICreateItemCommandHandler<ConfirmEmailChangeCommand, UserViewModel>
{
    private readonly RoleSettings _roleSettings = roleSettingsOptions.Value;
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = userRegisteredAppRepository;
    private readonly IEmailChangeRepository _emailChangeRepository = emailChangeRepository;
    private readonly ICodeCheckService _codeCheckService = codeCheckService;
    private readonly ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> _createOutboxMessageCommandHandler =
        createOutboxMessageCommandHandler;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<UserViewModel> CreateItemAsync(
        ConfirmEmailChangeCommand command,
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

        var emailChange = await _emailChangeRepository.GetEntityByAlternateIdAsync(
            new EmailChangeEntity { UserId = user.Id },
            QueryCondition.MayOrMayNotExist,
            cancellationToken
        );

        var isPending = emailChange is not null && emailChange.Expires > _timeProvider.GetUtcNow().UtcDateTime;

        var outcome = await _codeCheckService.CheckAsync(
            appName,
            UserMessages.LoginCodeAction,
            isPending ? emailChange!.Id.ToString() : Guid.CreateVersion7().ToString(),
            command.Code,
            cancellationToken
        );

        if (!isPending)
            throw new InvalidDataException(ErrorMessages.Codes.NotAccepted);

        CodeOutcomes.EnsureVerified(outcome);

        if (await _userManager.FindByEmailAsync(emailChange!.NewEmail) is { } emailOwner && emailOwner.Id != user.Id)
            throw new InvalidDataException(ErrorMessages.Users.EmailNotChangedByAdmin);

        var userDetails =
            await _userRepository.GetUserDtoAsync(user.Id, appName, cancellationToken) ?? throw NoRecordException.ForIdentifier(user.Id);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var oldEmail = user.Email;

            await UserEmailUpdate.ApplyAsync(_userManager, user, emailChange.NewEmail, true);

            _emailChangeRepository.RemoveEntityItem(emailChange);

            await _createOutboxMessageCommandHandler.CreateItemAsync(
                new CreateOutboxMessageCommand(
                    UserMessages.ContactChanged,
                    UserMessages.ContactChange(
                        oldEmail,
                        string.IsNullOrWhiteSpace(oldEmail) ? user.PhoneNumber : null,
                        userDetails,
                        appName,
                        ContactChange.Email,
                        command.Changer.NoticeContext(user.Id, command.RequestContext),
                        _timeProvider.GetUtcNow().UtcDateTime
                    )
                ),
                userId,
                cancellationToken
            );

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
