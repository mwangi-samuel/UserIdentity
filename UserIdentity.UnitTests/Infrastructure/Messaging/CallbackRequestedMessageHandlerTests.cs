using System.Reflection;

using FakeItEasy;

using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Events;
using PolyzenKit.Common.Enums;
using PolyzenKit.Infrastructure.Messaging.Receiving;

using UserIdentity.Application.Core.Users.Events;
using UserIdentity.Infrastructure.Messaging;

using Xunit;

namespace UserIdentity.UnitTests.Infrastructure.Messaging;

public class CallbackRequestedMessageHandlerTests
{
  private readonly IEventHandler<UserRegisteredEvent> _eventHandler = A.Fake<IEventHandler<UserRegisteredEvent>>();

  [Fact]
  public void Handler_Reads_UserIdentitys_Own_Callback_Subject()
  {
    // Act
    var attribute = typeof(CallbackRequestedMessageHandler).GetCustomAttribute<MessageSubjectAttribute>();

    // Assert
    Assert.Equal("useridentity.callback.requested", attribute?.Subject);
  }

  [Fact]
  public async Task HandleAsync_Maps_The_App_And_The_Users_Attributes()
  {
    // Arrange
    var message = CreateMessage(new Dictionary<MessageAttributeKey, string>
    {
      [MessageAttributeKey.UserId] = "user-id",
      [MessageAttributeKey.FirstName] = "Test",
      [MessageAttributeKey.LastName] = "",
      [MessageAttributeKey.UserName] = "test.user",
      [MessageAttributeKey.RecipientPhone] = "0712345678",
      [MessageAttributeKey.RecipientEmail] = "test.user@example.com"
    });

    // Act
    await new CallbackRequestedMessageHandler(_eventHandler).HandleAsync(message, TestContext.Current.CancellationToken);

    // Assert
    A.CallTo(() => _eventHandler.HandleEventAsync(new UserRegisteredEvent
    {
      AppName = "TestApp",
      UserId = "user-id",
      FirstName = "Test",
      LastName = null,
      UserName = "test.user",
      PhoneNumber = "0712345678",
      UserEmail = "test.user@example.com"
    }, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task HandleAsync_Without_A_User_Id_Throws_InvalidDataException_So_The_Message_Is_Dropped()
  {
    // Arrange
    var message = CreateMessage(new Dictionary<MessageAttributeKey, string> { [MessageAttributeKey.FirstName] = "Test" });

    // Act & Assert
    await Assert.ThrowsAsync<InvalidDataException>(() => new CallbackRequestedMessageHandler(_eventHandler).HandleAsync(message, TestContext.Current.CancellationToken));

    A.CallTo(() => _eventHandler.HandleEventAsync(A<UserRegisteredEvent>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  private static MessageEvent CreateMessage(Dictionary<MessageAttributeKey, string> attributes) => new()
  {
    Action = MessageAction.WelcomeUser,
    CorrelationId = "user-id",
    RegisteredApp = "TestApp",
    Attributes = attributes
  };
}
