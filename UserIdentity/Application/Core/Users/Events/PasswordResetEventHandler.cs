using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Events;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Enums;
using PolyzenKit.Domain.RegisteredApps;

namespace UserIdentity.Application.Core.Users.Events;

public record PasswordResetEvent : IBaseEvent
{
  public string ResetRequestId { get; set; } = null!;

  public UserEventContent UserContent { get; set; } = null!;

  public RegisteredAppEntity RegisteredApp { get; set; } = null!;
}

public class PasswordResetEventHandler(
  IKafkaMessageProducer<string, MessageEvent> kafkaMessageProducer
  ) : IEventHandler<PasswordResetEvent>
{
  private readonly IKafkaMessageProducer<string, MessageEvent> _kafkaMessageProducer = kafkaMessageProducer;

  private const string _userUpdateEventTopicKey = "UserUpdated";

  public async Task HandleEventAsync(PasswordResetEvent eventItem, CancellationToken cancellationToken = default)
  {
    var messageEvent = MessageEventFactory.Create(
      eventItem.ResetRequestId,
      eventItem.RegisteredApp.AppName,
      eventItem.UserContent.ToMessageAttributes(),
      MessageAction.PASSWORD_RESET
    );

    await _kafkaMessageProducer.ProduceAsync(messageEvent.CorrelationId, messageEvent, _userUpdateEventTopicKey, cancellationToken);
  }
}
