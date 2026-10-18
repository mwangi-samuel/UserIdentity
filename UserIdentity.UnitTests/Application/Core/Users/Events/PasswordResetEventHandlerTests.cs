using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using PolyzenKit.Application.Core.Messages.Events;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Enums;
using PolyzenKit.Common.Extensions;
using PolyzenKit.Domain.RegisteredApps;

using UserIdentity.Application.Core.Users.Events;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Events;

public class PasswordResetEventHandlerTests
{
  private readonly IKafkaMessageProducer<string, MessageEvent> _kafkaMessageProducer;

  public PasswordResetEventHandlerTests()
  {
    _kafkaMessageProducer = A.Fake<IKafkaMessageProducer<string, MessageEvent>>();
  }

  [Fact]
  public async Task HandleEventAsync_ProducesPasswordResetMessage_ForResetRequestId()
  {
    var userContent = new UserEventContent
    {
      UserIdentityId = "user-id",
      FirstName = "John",
      LastName = "Doe",
      UserName = "jdoe",
      PhoneNumber = "123456789",
      UserEmail = "jdoe@example.com"
    };

    var eventItem = new PasswordResetEvent
    {
      ResetRequestId = "reset-request-id",
      UserContent = userContent,
      RegisteredApp = new RegisteredAppEntity { AppName = "TestApp" }
    };

    var handler = new PasswordResetEventHandler(_kafkaMessageProducer);

    await handler.HandleEventAsync(eventItem, TestContext.Current.CancellationToken);

    A.CallTo(() => _kafkaMessageProducer.ProduceAsync(
      eventItem.ResetRequestId,
      A<MessageEvent>.That.Matches(m => IsExpectedMessage(m, eventItem)),
      "UserUpdated",
      A<CancellationToken>._))
    .MustHaveHappenedOnceExactly();
  }

  private static bool IsExpectedMessage(MessageEvent messageEvent, PasswordResetEvent eventItem)
  {
    if (messageEvent.Attributes is null)
      return false;

    return messageEvent.EventType == CrudEvent.CREATE &&
      messageEvent.CorrelationId == eventItem.ResetRequestId &&
      messageEvent.Action == MessageAction.PASSWORD_RESET &&
      messageEvent.RegisteredApp == eventItem.RegisteredApp.AppName &&
      messageEvent.Attributes.GetAttributeValue(MessageAttribute.RECIPIENT_EMAIL) == eventItem.UserContent.UserEmail &&
      messageEvent.Attributes.GetAttributeValue(MessageAttribute.RECIPIENT_PHONE) == eventItem.UserContent.PhoneNumber &&
      messageEvent.Attributes.GetAttributeValue(MessageAttribute.FIRST_NAME) == eventItem.UserContent.FirstName &&
      messageEvent.Attributes.GetAttributeValue(MessageAttribute.LAST_NAME) == eventItem.UserContent.LastName &&
      messageEvent.Attributes.GetAttributeValue(MessageAttribute.USER_NAME) == eventItem.UserContent.UserName &&
      messageEvent.Attributes.GetAttributeValue(MessageAttribute.USER_ID) == eventItem.UserContent.UserIdentityId;
  }
}
