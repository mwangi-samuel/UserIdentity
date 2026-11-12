using FakeItEasy;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Events;
using UserIdentity.Application.Core.Users.Events;
using UserIdentity.Infrastructure.Messaging;
using Xunit;

namespace UserIdentity.UnitTests.Infrastructure.Messaging;

public class CodeVerifiedMessageHandlerTests
{
    private readonly IEventHandler<CodeVerifiedEvent> _eventHandler = A.Fake<IEventHandler<CodeVerifiedEvent>>();

    private static readonly MessageEvent _messageEvent = CreateMessage(new { Status = "Verified", Channel = "Email" });

    [Fact]
    public async Task HandleAsync_For_A_Verified_Code_Passes_The_Action_And_Correlation_Id_To_The_Event_Handler()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var handler = new CodeVerifiedMessageHandler(_eventHandler);

        // Act
        await handler.HandleAsync(_messageEvent, cancellationTokenSource.Token);

        // Assert
        A.CallTo(() =>
                _eventHandler.HandleEventAsync(new CodeVerifiedEvent("WelcomeUser", "correlation-id"), cancellationTokenSource.Token)
            )
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData("Expired")]
    [InlineData("Failed")]
    [InlineData("")]
    public async Task HandleAsync_With_Another_Status_Does_Nothing(string status)
    {
        // Arrange
        var message = CreateMessage(new { Status = status, Channel = "Email" });
        var handler = new CodeVerifiedMessageHandler(_eventHandler);

        // Act
        await handler.HandleAsync(message, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _eventHandler.HandleEventAsync(A<CodeVerifiedEvent>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task HandleAsync_Without_A_Status_Does_Nothing()
    {
        // Arrange
        var message = CreateMessage(data: null);
        var handler = new CodeVerifiedMessageHandler(_eventHandler);

        // Act
        await handler.HandleAsync(message, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _eventHandler.HandleEventAsync(A<CodeVerifiedEvent>._, A<CancellationToken>._)).MustNotHaveHappened();
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

    private static MessageEvent CreateMessage(object? data) => MessageEventFactory.Create("correlation-id", "TestApp", "WelcomeUser", data);
}
