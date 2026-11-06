using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Enums;
using PolyzenKit.Common.Utilities;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.ViewModels;

namespace UserIdentity.Application.Core.Users.Events;

public record CodeVerifiedEvent(MessageAction Action, string CorrelationId) : IBaseEvent;

public class CodeVerifiedEventHandler(
    IUpdateItemCommandHandler<ConfirmUserCommand, ConfirmUserViewModel> confirmUserCommandHandler,
    IUpdateItemCommandHandler<VerifyPasswordResetCommand, VerifyPasswordResetViewModel> verifyPasswordResetCommandHandler
) : IEventHandler<CodeVerifiedEvent>
{
    private readonly IUpdateItemCommandHandler<ConfirmUserCommand, ConfirmUserViewModel> _confirmUserCommandHandler =
        confirmUserCommandHandler;
    private readonly IUpdateItemCommandHandler<
        VerifyPasswordResetCommand,
        VerifyPasswordResetViewModel
    > _verifyPasswordResetCommandHandler = verifyPasswordResetCommandHandler;

    public async Task HandleEventAsync(CodeVerifiedEvent eventItem, CancellationToken cancellationToken = default)
    {
        switch (eventItem.Action)
        {
            case MessageAction.WelcomeUser:
                await _confirmUserCommandHandler.UpdateItemAsync(
                    new ConfirmUserCommand { UserId = eventItem.CorrelationId },
                    KnownUsers.SystemId,
                    cancellationToken
                );
                break;

            case MessageAction.PasswordReset:
                await _verifyPasswordResetCommandHandler.UpdateItemAsync(
                    new VerifyPasswordResetCommand { ResetRequestId = eventItem.CorrelationId },
                    KnownUsers.SystemId,
                    cancellationToken
                );
                break;
        }
    }
}
