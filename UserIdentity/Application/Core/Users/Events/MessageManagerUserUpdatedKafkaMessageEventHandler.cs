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
  IUpdateItemCommandHandler<ConfirmUserCommand, ConfirmUserViewModel> updateItemCommandHandler
  ) : IEventHandler<MessageManagerUserUpdatedKafkaMessageEvent>
{
  private readonly IUpdateItemCommandHandler<ConfirmUserCommand, ConfirmUserViewModel> _updateItemCommandHandler = updateItemCommandHandler;

  public async Task HandleEventAsync(MessageManagerUserUpdatedKafkaMessageEvent eventItem, CancellationToken cancellationToken = default)
  {
    if (eventItem.MessageValue.Action == MessageAction.WELCOME_USER)
    {
      var command = new ConfirmUserCommand
      {
        UserId = eventItem.MessageValue.CorrelationId
      };

      await _updateItemCommandHandler.UpdateItemAsync(command, eventItem.MessageKey, cancellationToken);
    }
  }
}
