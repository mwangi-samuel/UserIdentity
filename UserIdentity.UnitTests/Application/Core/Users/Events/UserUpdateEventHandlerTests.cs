using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using PolyzenKit.Application.Core.Messages.Events;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Enums;
using PolyzenKit.Common.Extensions;
using PolyzenKit.Domain.RegisteredApps;

using UserIdentity.Application.Core.Users.Events;
using UserIdentity.Application.Enums;
using UserIdentity.Application.Interfaces;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Events;

public class UserUpdateEventHandlerTests
{
  private readonly IAppCallbackService _appCallbackService;
  private readonly IKafkaMessageProducer<string, MessageEvent> _kafkaMessageProducer;

  public UserUpdateEventHandlerTests()
  {
    _appCallbackService = A.Fake<IAppCallbackService>();
    _kafkaMessageProducer = A.Fake<IKafkaMessageProducer<string, MessageEvent>>();
  }

  [Fact]
  public async Task HandleEventAsync_WhenCreateRequestFromUi_SendsCallbackAndProducesWelcomeMessage()
  {
    var userContent = new UserEventContent
    {
      UserIdentityId = "user-id",
      FirstName = "John",
      LastName = "Doe",
      UserName = "jdoe",
      PhoneNumber = "123456789",
      PhoneNumberConfirmed = true,
      UserEmail = "jdoe@example.com",
      EmailConfirmed = true
    };

    var registeredApp = new RegisteredAppEntity
    {
      AppName = "TestApp",
      CallbackUrl = "https://callback.test"
    };

    var eventItem = new UserUpdateEvent
    {
      RequestSource = RequestSource.UI,
      EventType = CrudEvent.CREATE,
      UserContent = userContent,
      RegisteredApp = registeredApp
    };

    var handler = new UserUpdateEventHandler(_appCallbackService, _kafkaMessageProducer);

    await handler.HandleEventAsync(eventItem, TestContext.Current.CancellationToken);

    A.CallTo(() => _appCallbackService.SendCallbackRequestAsync(eventItem, A<CancellationToken>._))
      .MustHaveHappenedOnceExactly();

    A.CallTo(() => _kafkaMessageProducer.ProduceAsync(
      userContent.UserIdentityId!,
      A<MessageEvent>.That.Matches(m => IsExpectedMessage(m, userContent, registeredApp)),
      "UserUpdated",
      A<CancellationToken>._))
    .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task HandleEventAsync_WhenEventIsNotCreate_DoesNotPublishKafkaMessage()
  {
    var userContent = new UserEventContent
    {
      UserIdentityId = "user-id"
    };

    var registeredApp = new RegisteredAppEntity
    {
      AppName = "TestApp",
      CallbackUrl = "https://callback.test"
    };

    var eventItem = new UserUpdateEvent
    {
      RequestSource = RequestSource.UI,
      EventType = (CrudEvent)999,
      UserContent = userContent,
      RegisteredApp = registeredApp
    };

    var handler = new UserUpdateEventHandler(_appCallbackService, _kafkaMessageProducer);

    await handler.HandleEventAsync(eventItem, TestContext.Current.CancellationToken);

    A.CallTo(() => _appCallbackService.SendCallbackRequestAsync(eventItem, A<CancellationToken>._))
      .MustHaveHappenedOnceExactly();

    A.CallTo(() => _kafkaMessageProducer.ProduceAsync(A<string>._, A<MessageEvent>._, A<string>._, A<CancellationToken>._))
      .MustNotHaveHappened();
  }

  private static bool IsExpectedMessage(MessageEvent messageEvent, UserEventContent userContent, RegisteredAppEntity registeredApp)
  {
    if (messageEvent.Attributes is null)
      return false;

    return messageEvent.EventType == CrudEvent.CREATE &&
      messageEvent.CorrelationId == userContent.UserIdentityId &&
      messageEvent.Action == MessageAction.WELCOME_USER &&
      messageEvent.RegisteredApp == registeredApp.AppName &&
      messageEvent.Attributes.GetAttributeValue(MessageAttribute.RECIPIENT_EMAIL) == userContent.UserEmail &&
      messageEvent.Attributes.GetAttributeValue(MessageAttribute.RECIPIENT_PHONE) == userContent.PhoneNumber &&
      messageEvent.Attributes.GetAttributeValue(MessageAttribute.FIRST_NAME) == userContent.FirstName &&
      messageEvent.Attributes.GetAttributeValue(MessageAttribute.LAST_NAME) == userContent.LastName &&
      messageEvent.Attributes.GetAttributeValue(MessageAttribute.USER_NAME) == userContent.UserName &&
      messageEvent.Attributes.GetAttributeValue(MessageAttribute.USER_ID) == userContent.UserIdentityId;
  }
}
