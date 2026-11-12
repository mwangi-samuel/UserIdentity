using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Utilities;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;

namespace UserIdentity.Application.Core.Users.Events;

public record CodeVerifiedEvent(string Action, string CorrelationId) : IBaseEvent;

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
            case UserMessages.WelcomeUserAction:
                await _confirmUserCommandHandler.UpdateItemAsync(
                    new ConfirmUserCommand { UserId = eventItem.CorrelationId },
                    KnownUsers.SystemId,
                    cancellationToken
                );
                break;

            case UserMessages.PasswordResetAction:
                await _verifyPasswordResetCommandHandler.UpdateItemAsync(
                    new VerifyPasswordResetCommand { ResetRequestId = eventItem.CorrelationId },
                    KnownUsers.SystemId,
                    cancellationToken
                );
                break;
        }
    }
}
