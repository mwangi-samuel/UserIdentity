using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Events;
using PolyzenKit.Common.Enums;

using UserIdentity.Application.Core.Users.Events;
using UserIdentity.Infrastructure.Kafka;

using Xunit;

namespace UserIdentity.UnitTests.Infrastructure.Kafka;

public class MessageManagerUserUpdatedKafkaMessageConsumerTests
{
  private readonly IEventHandler<MessageManagerUserUpdatedKafkaMessageEvent> _eventHandler;

  private static readonly MessageEvent _messageEvent = new()
  {
    EventType = CrudEvent.CREATE,
    Action = MessageAction.WELCOME_USER,
    CorrelationId = "correlation-id",
    RegisteredApp = "app",
    Attributes = new Dictionary<MessageAttribute, string>()
  };

  public MessageManagerUserUpdatedKafkaMessageConsumerTests()
  {
    _eventHandler = A.Fake<IEventHandler<MessageManagerUserUpdatedKafkaMessageEvent>>();
  }

  [Fact]
  public async Task ConsumeAsync_InvokesEventHandler()
  {
    using var cancellationTokenSource = new CancellationTokenSource();
    var consumer = new MessageManagerUserUpdatedKafkaMessageConsumer(_eventHandler);

    await consumer.ConsumeAsync("key", _messageEvent, cancellationTokenSource.Token);

    A.CallTo(() => _eventHandler.HandleEventAsync(
      A<MessageManagerUserUpdatedKafkaMessageEvent>.That.Matches(e => e.MessageKey == "key" && e.MessageValue == _messageEvent),
      cancellationTokenSource.Token))
      .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task ConsumeAsync_WhenHandlerThrows_Rethrows_So_The_Consumer_Host_Retries()
  {
    A.CallTo(() => _eventHandler.HandleEventAsync(A<MessageManagerUserUpdatedKafkaMessageEvent>._, A<CancellationToken>._))
      .ThrowsAsync(new InvalidOperationException("boom"));

    var consumer = new MessageManagerUserUpdatedKafkaMessageConsumer(_eventHandler);

    var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.ConsumeAsync("key", _messageEvent));

    Assert.Equal("boom", exception.Message);
  }
}
