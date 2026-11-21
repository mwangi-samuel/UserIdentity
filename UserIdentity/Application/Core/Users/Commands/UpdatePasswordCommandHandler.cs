using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Commands;
using PolyzenKit.Application.Core.Messages.ViewModels;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Application.Interfaces;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.PasswordResets;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.LoginTickets;
using UserIdentity.Persistence.Repositories.PasswordResets;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using UserIdentity.Persistence.Repositories.Users;

namespace UserIdentity.Application.Core.Users.Commands;

public record UpdatePasswordCommand : IBaseCommand
{
    [JsonIgnore]
    public RegisteredAppDto RegisteredApp { get; internal set; } = null!;

    [JsonIgnore]
    public RequestContext RequestContext { get; internal set; } = new();

    [Required]
    public string ResetRequestId { get; init; } = null!;

    [Required]
    public string Code { get; init; } = null!;

    [Required]
    public string NewPassword { get; init; } = null!;
}

public class UpdatePasswordCommandHandler(
    UserManager<IdentityUser> userManager,
    ITokenFactory tokenFactory,
    IUnitOfWork unitOfWork,
    IPasswordResetRepository passwordResetRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ILoginTicketRepository loginTicketRepository,
    IUserRepository userRepository,
    ICodeCheckService codeCheckService,
    ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> createOutboxMessageCommandHandler,
    ICreateItemCommandHandler<SignInUserCommand, AuthUserViewModel> signInUserCommandHandler,
    TimeProvider timeProvider
)
    : BaseAuditableItemCommandHandler<Guid, PasswordResetEntity, IPasswordResetRepository>(
        unitOfWork,
        passwordResetRepository,
        timeProvider
    ),
        IUpdateItemCommandHandler<UpdatePasswordCommand, AuthUserViewModel>
{
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly ITokenFactory _tokenFactory = tokenFactory;
    private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
    private readonly ILoginTicketRepository _loginTicketRepository = loginTicketRepository;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly ICodeCheckService _codeCheckService = codeCheckService;
    private readonly ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> _createOutboxMessageCommandHandler =
        createOutboxMessageCommandHandler;
    private readonly ICreateItemCommandHandler<SignInUserCommand, AuthUserViewModel> _signInUserCommandHandler = signInUserCommandHandler;

    public async Task<AuthUserViewModel> UpdateItemAsync(
        UpdatePasswordCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var registeredApp = command.RegisteredApp;

        var passwordReset = await Repository.GetEntityByAlternateIdAsync(
            new PasswordResetEntity { RequestId = _tokenFactory.HashToken(command.ResetRequestId), AppId = registeredApp.Id },
            QueryCondition.MayOrMayNotExist,
            cancellationToken
        );

        var user =
            passwordReset is null || passwordReset.Applied || passwordReset.Expires <= TimeProvider.GetUtcNow().UtcDateTime
                ? null
                : await _userManager.FindByIdAsync(passwordReset.UserId);

        await PasswordStrength.EnsureValidAsync(_userManager, user ?? new IdentityUser(), command.NewPassword);

        var outcome = await _codeCheckService.CheckAsync(
            registeredApp.AppName,
            UserMessages.PasswordResetAction,
            user is null ? Guid.CreateVersion7().ToString() : passwordReset!.Id.ToString(),
            command.Code,
            cancellationToken
        );

        if (user is null)
            throw new InvalidDataException(ErrorMessages.Codes.NotAccepted);

        CodeOutcomes.EnsureVerified(outcome);

        await UnitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var resetPasswordToken = await _userManager.GeneratePasswordResetTokenAsync(user);

            user.LockoutEnd = null;
            user.AccessFailedCount = 0;

            var resetPasswordResult = await _userManager.ResetPasswordAsync(user, resetPasswordToken, command.NewPassword);

            if (!resetPasswordResult.Succeeded)
                throw new InvalidDataException(string.Join(" ", resetPasswordResult.Errors.Select(e => e.Description)));

            passwordReset!.Applied = true;

            await UpdateEntityItemAsync(passwordReset, user.Id, cancellationToken);

            await _refreshTokenRepository.DeleteEntityItemsAsync(user.Id, cancellationToken: cancellationToken);
            await _loginTicketRepository.DeleteEntityItemsAsync(user.Id, cancellationToken);

            var userDetails = await _userRepository.GetUserDtoAsync(user.Id, null, cancellationToken) ?? new UserDto();

            await _createOutboxMessageCommandHandler.CreateItemAsync(
                new CreateOutboxMessageCommand(
                    UserMessages.PasswordChanged,
                    UserMessages.PasswordChange(
                        user,
                        userDetails,
                        registeredApp.AppName,
                        command.RequestContext,
                        TimeProvider.GetUtcNow().UtcDateTime
                    )
                ),
                user.Id,
                cancellationToken
            );

            var vm = await _signInUserCommandHandler.CreateItemAsync(
                new SignInUserCommand(user, registeredApp, command.RequestContext) { EmailProven = true },
                user.Id,
                cancellationToken
            );

            await UnitOfWork.CommitTransactionAsync(cancellationToken);

            return vm;
        }
        catch
        {
            await UnitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
    }
}
