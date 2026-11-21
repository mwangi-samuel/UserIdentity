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
using PolyzenKit.Domain.Entities;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.TwoFactor.Shared;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Common.Validation;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.LoginTickets;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.TwoFactor.Commands;

public record ResetUserTwoFactorCommand : IBaseCommand
{
    [JsonIgnore]
    public RegisteredAppDto RegisteredApp { get; internal set; } = null!;

    [JsonIgnore]
    public UserChanger Changer { get; internal set; } = null!;

    [
        Required,
        StringLength(UserLimits.EmailMaxLength),
        RegularExpression(ValidationPatterns.Email, ErrorMessage = ErrorMessages.Users.InvalidEmail)
    ]
    public string Email { get; init; } = null!;
}

public class ResetUserTwoFactorCommandHandler(
    IOptions<RoleSettings> roleSettingsOptions,
    UserManager<IdentityUser> userManager,
    IUnitOfWork unitOfWork,
    IUserRepository userRepository,
    IUserRegisteredAppRepository userRegisteredAppRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ILoginTicketRepository loginTicketRepository,
    ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> createOutboxMessageCommandHandler,
    TimeProvider timeProvider
) : IUpdateItemCommandHandler<ResetUserTwoFactorCommand, UserViewModel>
{
    private readonly RoleSettings _roleSettings = roleSettingsOptions.Value;
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = userRegisteredAppRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
    private readonly ILoginTicketRepository _loginTicketRepository = loginTicketRepository;
    private readonly ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> _createOutboxMessageCommandHandler =
        createOutboxMessageCommandHandler;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<UserViewModel> UpdateItemAsync(
        ResetUserTwoFactorCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var appName = command.RegisteredApp.AppName;

        var user = await UserChangeAccess.EnsureAllowedAsync(
            _userManager,
            _userRegisteredAppRepository,
            _roleSettings,
            await _userManager.FindByEmailAsync(command.Email),
            command.Email,
            appName,
            command.Changer,
            cancellationToken
        );

        var userDetails = await _userRepository.GetEntityItemAsync(user.Id, cancellationToken);
        var previousMethod = userDetails.TwoFactorMethod;
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            await TwoFactorCredentials.RemoveAsync(_userManager, user);

            userDetails.TwoFactorMethod = null;
            userDetails.UpdateEntityAuditFields(userId, now);

            await _refreshTokenRepository.DeleteEntityItemsAsync(user.Id, cancellationToken);
            await _loginTicketRepository.DeleteEntityItemsAsync(user.Id, cancellationToken);

            await _createOutboxMessageCommandHandler.CreateItemAsync(
                new CreateOutboxMessageCommand(
                    UserMessages.TwoFactorChanged,
                    UserMessages.TwoFactorChange(
                        user,
                        userDetails,
                        appName,
                        TwoFactorChange.Reset,
                        previousMethod,
                        new RequestContext(),
                        now
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
