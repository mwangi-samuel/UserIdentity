using System.Reflection;
using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Events;
using PolyzenKit.Infrastructure.Messaging.Receiving;
using UserIdentity.Application.Core.Users.Events;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Domain.Users;
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
    public async Task HandleAsync_Maps_The_App_The_Recipient_And_The_Users_Data()
    {
        // Arrange
        var message = CreateMessage(
            new
            {
                UserId = "user-id",
                UserName = "test.user",
                FirstName = "Test",
            },
            new MessageRecipient
            {
                Email = "test.user@example.com",
                Phone = "0712345678",
                Name = "Test",
            }
        );

        // Act
        await new CallbackRequestedMessageHandler(_eventHandler).HandleAsync(message, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() =>
                _eventHandler.HandleEventAsync(
                    new UserRegisteredEvent
                    {
                        AppName = "TestApp",
                        UserId = "user-id",
                        FirstName = "Test",
                        LastName = null,
                        UserName = "test.user",
                        PhoneNumber = "0712345678",
                        UserEmail = "test.user@example.com",
                    },
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task HandleAsync_Reads_The_Message_That_Sign_Up_Sends()
    {
        // Arrange
        var user = new IdentityUser
        {
            Id = "user-id",
            UserName = "test.user",
            Email = "test.user@example.com",
            PhoneNumber = "0712345678",
        };
        var userDetails = new UserEntity
        {
            Id = "user-id",
            FirstName = "Test",
            LastName = "User",
        };
        var message = UserMessages.Welcome(user, userDetails, "TestApp");

        // Act
        await new CallbackRequestedMessageHandler(_eventHandler).HandleAsync(message, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() =>
                _eventHandler.HandleEventAsync(
                    new UserRegisteredEvent
                    {
                        AppName = "TestApp",
                        UserId = "user-id",
                        FirstName = "Test",
                        LastName = "User",
                        UserName = "test.user",
                        PhoneNumber = "0712345678",
                        UserEmail = "test.user@example.com",
                    },
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task HandleAsync_Without_A_Recipient_Leaves_The_Phone_And_Email_Out()
    {
        // Arrange
        var message = CreateMessage(new { UserId = "user-id" });

        // Act
        await new CallbackRequestedMessageHandler(_eventHandler).HandleAsync(message, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() =>
                _eventHandler.HandleEventAsync(new UserRegisteredEvent { AppName = "TestApp", UserId = "user-id" }, A<CancellationToken>._)
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task HandleAsync_Without_A_User_Id_Throws_InvalidDataException_So_The_Message_Is_Dropped()
    {
        // Arrange
        var message = CreateMessage(new { FirstName = "Test" });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new CallbackRequestedMessageHandler(_eventHandler).HandleAsync(message, TestContext.Current.CancellationToken)
        );

        A.CallTo(() => _eventHandler.HandleEventAsync(A<UserRegisteredEvent>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    private static MessageEvent CreateMessage(object data, MessageRecipient? recipient = null) =>
        MessageEventFactory.Create("user-id", "TestApp", UserMessages.WelcomeUserAction, data, recipient);
}
