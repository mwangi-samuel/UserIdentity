using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Commands;
using PolyzenKit.Application.Core.Messages.ViewModels;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Domain.Entities;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Domain.PasswordResets;
using UserIdentity.Persistence.Repositories.PasswordResets;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.Users.Commands;

public record ResetPasswordCommand : IBaseCommand
{
    [JsonIgnore]
    public ActiveRegisteredApp RegisteredApp { get; internal set; } = null!;

    [Required]
    public string UserName { get; init; } = null!;
}

public class ResetPasswordCommandHandler(
    IOptions<PasswordResetSettings> passwordResetSettingsOptions,
    UserManager<IdentityUser> userManager,
    TimeProvider timeProvider,
    ITokenFactory tokenFactory,
    IUnitOfWork unitOfWork,
    IUserRepository userRepository,
    IPasswordResetRepository passwordResetRepository,
    ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> createOutboxMessageCommandHandler,
    IUserRegisteredAppRepository userRegisteredAppRepository
)
    : BaseAuditableItemCommandHandler<Guid, PasswordResetEntity, IPasswordResetRepository>(
        unitOfWork,
        passwordResetRepository,
        timeProvider
    ),
        ICreateItemCommandHandler<ResetPasswordCommand, ResetPasswordViewModel>
{
    private readonly PasswordResetSettings _passwordResetSettings = passwordResetSettingsOptions.Value;
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly ITokenFactory _tokenFactory = tokenFactory;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = userRegisteredAppRepository;
    private readonly ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> _createOutboxMessageCommandHandler =
        createOutboxMessageCommandHandler;

    public const string DefaultResetPasswordMessage =
        "If an account matches the details provided, you will receive a code to reset your password.";

    public async Task<ResetPasswordViewModel> CreateItemAsync(
        ResetPasswordCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        // Same response whether or not the user exists
        var resetRequestId = _tokenFactory.GenerateToken();

        var vm = new ResetPasswordViewModel
        {
            ResetPasswordDetails = new ResetPasswordDto { EmailMessage = DefaultResetPasswordMessage, ResetRequestId = resetRequestId },
        };

        var existingUser = await _userManager.FindByNameAsync(command.UserName) ?? await _userManager.FindByEmailAsync(command.UserName);

        if (
            existingUser is null
            || !await _userRegisteredAppRepository.IsUserRegisteredForAppAsync(
                existingUser.Id,
                command.RegisteredApp.AppName,
                cancellationToken
            )
        )
            return vm;

        await UnitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var userEntity = await _userRepository.GetEntityItemForUpdateAsync(existingUser.Id, cancellationToken);
            var now = TimeProvider.GetUtcNow().UtcDateTime;

            (var latestCreatedAt, var requestsInWindow) = await Repository.GetRequestHistoryAsync(
                existingUser.Id,
                now - PasswordResetLimits.RequestCountWindow,
                cancellationToken
            );

            // Each request gets its own code and attempts, so the requests per day cap the guesses per day
            if (
                userEntity is null
                || latestCreatedAt > now.AddSeconds(-_passwordResetSettings.RequestCooldownSeconds)
                || requestsInWindow >= _passwordResetSettings.MaxRequestsPerDay
            )
            {
                await UnitOfWork.RollbackTransactionAsync(CancellationToken.None);

                return vm;
            }

            var passwordResetEntity = new PasswordResetEntity
            {
                RequestId = _tokenFactory.HashToken(resetRequestId),
                UserId = existingUser.Id,
                Expires = now.AddSeconds(_passwordResetSettings.ValidForSeconds),
            };

            passwordResetEntity.SetEntityAuditFields(existingUser.Id, now);

            await _createOutboxMessageCommandHandler.CreateItemAsync(
                new CreateOutboxMessageCommand(
                    UserMessages.PasswordResetRequested,
                    UserMessages.PasswordReset(resetRequestId, existingUser, userEntity, command.RegisteredApp.AppName)
                ),
                existingUser.Id,
                cancellationToken
            );

            Repository.CreateEntityItem(passwordResetEntity);

            await UnitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await UnitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }

        return vm;
    }
}
