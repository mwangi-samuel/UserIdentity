using FakeItEasy;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Events;
using PolyzenKit.Common.Enums;
using UserIdentity.Application.Core.Users.Events;
using UserIdentity.Infrastructure.Messaging;
using Xunit;

namespace UserIdentity.UnitTests.Infrastructure.Messaging;

public class CodeVerifiedMessageHandlerTests
{
    private readonly IEventHandler<CodeVerifiedEvent> _eventHandler = A.Fake<IEventHandler<CodeVerifiedEvent>>();

    private static readonly MessageEvent _messageEvent = new()
    {
        Action = MessageAction.WelcomeUser,
        CorrelationId = "correlation-id",
        RegisteredApp = "TestApp",
        Attributes = new Dictionary<MessageAttributeKey, string>(),
    };

    [Fact]
    public async Task HandleAsync_Passes_The_Action_And_Correlation_Id_To_The_Event_Handler()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var handler = new CodeVerifiedMessageHandler(_eventHandler);

        // Act
        await handler.HandleAsync(_messageEvent, cancellationTokenSource.Token);

        // Assert
        A.CallTo(() =>
                _eventHandler.HandleEventAsync(
                    new CodeVerifiedEvent(MessageAction.WelcomeUser, "correlation-id"),
                    cancellationTokenSource.Token
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task HandleAsync_When_The_Event_Handler_Throws_Rethrows_So_The_Message_Is_Retried()
    {
        // Arrange
        A.CallTo(() => _eventHandler.HandleEventAsync(A<CodeVerifiedEvent>._, A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var handler = new CodeVerifiedMessageHandler(_eventHandler);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(_messageEvent, TestContext.Current.CancellationToken)
        );

        Assert.Equal("boom", exception.Message);
    }
}
