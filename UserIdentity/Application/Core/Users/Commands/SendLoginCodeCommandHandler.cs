using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Commands;
using PolyzenKit.Application.Core.Messages.ViewModels;
using UserIdentity.Application.Core.AccountLimits.Commands;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Common.Enums;
using UserIdentity.Domain.Users;

namespace UserIdentity.Application.Core.Users.Commands;

public record SendLoginCodeCommand : IBaseCommand
{
    public required string UserId { get; init; }

    public required UserEntity UserDetails { get; init; }

    public required string AppName { get; init; }

    public required LoginCodePurpose Purpose { get; init; }

    public required string CorrelationId { get; init; }

    public required DateTime ExpiresAt { get; init; }

    public string? Email { get; init; }

    public string? PhoneNumber { get; init; }

    public required RequestContext RequestContext { get; init; }
}

public class SendLoginCodeCommandHandler(
    ICreateItemCommandHandler<CreateAccountLimitEventCommand, AccountLimitViewModel> createAccountLimitEventCommandHandler,
    ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> createOutboxMessageCommandHandler,
    TimeProvider timeProvider
) : ICreateItemCommandHandler<SendLoginCodeCommand, LoginCodeViewModel>
{
    private readonly ICreateItemCommandHandler<
        CreateAccountLimitEventCommand,
        AccountLimitViewModel
    > _createAccountLimitEventCommandHandler = createAccountLimitEventCommandHandler;
    private readonly ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> _createOutboxMessageCommandHandler =
        createOutboxMessageCommandHandler;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<LoginCodeViewModel> CreateItemAsync(
        SendLoginCodeCommand command,
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        var limit = await _createAccountLimitEventCommandHandler.CreateItemAsync(
            new CreateAccountLimitEventCommand(command.UserId, AccountLimitKind.LoginCodeSent),
            userId,
            cancellationToken
        );

        if (limit.IsReached)
            return new LoginCodeViewModel { Sent = false };

        var loginCode = UserMessages.LoginCode(
            command.CorrelationId,
            command.ExpiresAt,
            command.Purpose,
            command.Email,
            command.PhoneNumber,
            command.UserDetails,
            command.AppName,
            command.RequestContext,
            _timeProvider.GetUtcNow().UtcDateTime
        );

        await _createOutboxMessageCommandHandler.CreateItemAsync(
            new CreateOutboxMessageCommand(UserMessages.LoginCodeRequested, loginCode),
            userId,
            cancellationToken
        );

        return new LoginCodeViewModel { Sent = true, CodeSentTo = ContactMask.For(command.Email, command.PhoneNumber) };
    }
}
