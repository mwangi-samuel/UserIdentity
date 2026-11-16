using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Domain.PasswordResets;
using UserIdentity.Persistence.Repositories.PasswordResets;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.Users.Commands;

public record UpdatePasswordCommand : IBaseCommand
{
    [JsonIgnore]
    public ActiveRegisteredApp RegisteredApp { get; internal set; } = null!;

    [Required]
    public string ResetRequestId { get; init; } = null!;

    [Required]
    public string NewPassword { get; init; } = null!;
}

public class UpdatePasswordCommandHandler(
    UserManager<IdentityUser> userManager,
    ITokenFactory tokenFactory,
    IUnitOfWork unitOfWork,
    IPasswordResetRepository passwordResetRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IUserRepository userRepository,
    ICreateItemCommandHandler<StartSessionCommand, UserTokenViewModel> startSessionCommandHandler,
    TimeProvider timeProvider
)
    : BaseAuditableItemCommandHandler<Guid, PasswordResetEntity, IPasswordResetRepository>(
        unitOfWork,
        passwordResetRepository,
        timeProvider
    ),
        IUpdateItemCommandHandler<UpdatePasswordCommand, UpdatePasswordViewModel>
{
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly ITokenFactory _tokenFactory = tokenFactory;
    private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly ICreateItemCommandHandler<StartSessionCommand, UserTokenViewModel> _startSessionCommandHandler =
        startSessionCommandHandler;

    public async Task<UpdatePasswordViewModel> UpdateItemAsync(
        UpdatePasswordCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var passwordResetEntity = new PasswordResetEntity { RequestId = _tokenFactory.HashToken(command.ResetRequestId) };

        var existingEntity = await Repository.GetEntityByAlternateIdAsync(
            passwordResetEntity,
            QueryCondition.MayOrMayNotExist,
            cancellationToken
        );

        if (
            existingEntity is null
            || !existingEntity.Verified
            || existingEntity.Applied
            || existingEntity.Expires <= TimeProvider.GetUtcNow().UtcDateTime
        )
            return GetUpdatePasswordViewModel(false);

        var userDetails = await _userManager.FindByIdAsync(existingEntity.UserId);

        if (userDetails is null)
            return GetUpdatePasswordViewModel(false);

        var appName = command.RegisteredApp.AppName;
        var canSignIn =
            AccountConfirmation.IsConfirmed(command.RegisteredApp, userDetails)
            && await _userRepository.GetUserDtoAsync(userDetails.Id, appName, cancellationToken) is not null;
        UserTokenViewModel? session = null;

        // Start DB Transaction
        await UnitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var resetPasswordToken = await _userManager.GeneratePasswordResetTokenAsync(userDetails);

            userDetails.LockoutEnd = null;
            userDetails.AccessFailedCount = 0;

            var resetPasswordResult = await _userManager.ResetPasswordAsync(userDetails, resetPasswordToken, command.NewPassword);

            if (!resetPasswordResult.Succeeded)
                throw new InvalidDataException(string.Join(" ", resetPasswordResult.Errors.Select(e => e.Description)));

            existingEntity.Applied = true;

            await UpdateEntityItemAsync(existingEntity, userDetails.Id, cancellationToken);

            await _refreshTokenRepository.DeleteEntityItemsAsync(userDetails.Id, cancellationToken);

            if (canSignIn)
            {
                session = await _startSessionCommandHandler.CreateItemAsync(
                    new StartSessionCommand(userDetails.Id, userDetails.UserName!, appName),
                    userDetails.Id,
                    cancellationToken
                );

                await UnitOfWork.SaveChangesAsync(cancellationToken);
            }

            // Commit DB Transaction
            await UnitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await UnitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }

        return GetUpdatePasswordViewModel(true, session?.UserToken);
    }

    private static UpdatePasswordViewModel GetUpdatePasswordViewModel(bool passwordUpdated, UserTokenDto? userToken = null) =>
        new()
        {
            UpdatePasswordResult = new UpdatePasswordDto { PasswordUpdated = passwordUpdated },
            UserToken = userToken,
        };
}
