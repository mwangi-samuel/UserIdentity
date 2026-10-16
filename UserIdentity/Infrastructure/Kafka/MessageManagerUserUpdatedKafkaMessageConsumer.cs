using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Events;
using PolyzenKit.Application.Interfaces;

using UserIdentity.Application.Core.Users.Events;

namespace UserIdentity.Infrastructure.Kafka;

public class MessageManagerUserUpdatedKafkaMessageConsumer(
  IEventHandler<MessageManagerUserUpdatedKafkaMessageEvent> eventHandler
  ) : IKafkaMessageConsumer
{

  private readonly IEventHandler<MessageManagerUserUpdatedKafkaMessageEvent> _eventHandler = eventHandler;

  // Exceptions are deliberately not caught: the PolyzenKit consumer host logs each failed attempt,
  // retries it and only commits the offset once handling succeeds (or the retries run out)
  public async Task ConsumeAsync(string key, MessageEvent message, CancellationToken cancellationToken = default)
  {
    var consumedEvent = new MessageManagerUserUpdatedKafkaMessageEvent
    {
      MessageKey = key,
      MessageValue = message
    };

    await _eventHandler.HandleEventAsync(consumedEvent, cancellationToken);
  }
}
