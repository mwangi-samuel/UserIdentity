using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Events;
using PolyzenKit.Common.Enums;

using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.ViewModels;

namespace UserIdentity.Application.Core.Users.Events;

public record MessageManagerUserUpdatedKafkaMessageEvent : IBaseEvent
{
  public required string MessageKey { get; init; }

  public required MessageEvent MessageValue { get; init; }
}

public class MessageManagerUserUpdatedKafkaMessageEventHandler(
  IUpdateItemCommandHandler<ConfirmUserCommand, ConfirmUserViewModel> confirmUserCommandHandler,
  IUpdateItemCommandHandler<VerifyPasswordResetCommand, VerifyPasswordResetViewModel> verifyPasswordResetCommandHandler
  ) : IEventHandler<MessageManagerUserUpdatedKafkaMessageEvent>
{
  private readonly IUpdateItemCommandHandler<ConfirmUserCommand, ConfirmUserViewModel> _confirmUserCommandHandler = confirmUserCommandHandler;
  private readonly IUpdateItemCommandHandler<VerifyPasswordResetCommand, VerifyPasswordResetViewModel> _verifyPasswordResetCommandHandler = verifyPasswordResetCommandHandler;

  public async Task HandleEventAsync(MessageManagerUserUpdatedKafkaMessageEvent eventItem, CancellationToken cancellationToken = default)
  {
    switch (eventItem.MessageValue.Action)
    {
      case MessageAction.WELCOME_USER:
        var confirmUserCommand = new ConfirmUserCommand
        {
          UserId = eventItem.MessageValue.CorrelationId
        };

        await _confirmUserCommandHandler.UpdateItemAsync(confirmUserCommand, eventItem.MessageKey, cancellationToken);
        break;

      case MessageAction.PASSWORD_RESET:
        var verifyPasswordResetCommand = new VerifyPasswordResetCommand
        {
          ResetRequestId = eventItem.MessageValue.CorrelationId
        };

        await _verifyPasswordResetCommandHandler.UpdateItemAsync(verifyPasswordResetCommand, eventItem.MessageKey, cancellationToken);
        break;
    }
  }
}
