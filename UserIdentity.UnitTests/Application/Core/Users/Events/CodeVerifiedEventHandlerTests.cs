using FakeItEasy;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Enums;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Utilities;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Events;
using UserIdentity.Application.Core.Users.ViewModels;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Events;

public class CodeVerifiedEventHandlerTests
{
    private readonly IUpdateItemCommandHandler<ConfirmUserCommand, ConfirmUserViewModel> _confirmUserCommandHandler = A.Fake<
        IUpdateItemCommandHandler<ConfirmUserCommand, ConfirmUserViewModel>
    >();
    private readonly IUpdateItemCommandHandler<
        VerifyPasswordResetCommand,
        VerifyPasswordResetViewModel
    > _verifyPasswordResetCommandHandler = A.Fake<IUpdateItemCommandHandler<VerifyPasswordResetCommand, VerifyPasswordResetViewModel>>();

    [Fact]
    public async Task HandleEventAsync_For_A_Welcome_User_Code_Confirms_The_User()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        await handler.HandleEventAsync(new CodeVerifiedEvent(MessageAction.WelcomeUser, "user-id"), TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() =>
                _confirmUserCommandHandler.UpdateItemAsync(
                    A<ConfirmUserCommand>.That.Matches(c => c.UserId == "user-id"),
                    KnownUsers.SystemId,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() =>
                _verifyPasswordResetCommandHandler.UpdateItemAsync(A<VerifyPasswordResetCommand>._, A<string>._, A<CancellationToken>._)
            )
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task HandleEventAsync_For_A_Password_Reset_Code_Verifies_The_Reset_Request()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        await handler.HandleEventAsync(
            new CodeVerifiedEvent(MessageAction.PasswordReset, "reset-request-id"),
            TestContext.Current.CancellationToken
        );

        // Assert
        A.CallTo(() =>
                _verifyPasswordResetCommandHandler.UpdateItemAsync(
                    A<VerifyPasswordResetCommand>.That.Matches(c => c.ResetRequestId == "reset-request-id"),
                    KnownUsers.SystemId,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _confirmUserCommandHandler.UpdateItemAsync(A<ConfirmUserCommand>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task HandleEventAsync_For_Another_Action_Does_Nothing()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        await handler.HandleEventAsync(new CodeVerifiedEvent(MessageAction.OrderPlaced, "order-id"), TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _confirmUserCommandHandler.UpdateItemAsync(A<ConfirmUserCommand>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() =>
                _verifyPasswordResetCommandHandler.UpdateItemAsync(A<VerifyPasswordResetCommand>._, A<string>._, A<CancellationToken>._)
            )
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task HandleEventAsync_For_An_Unknown_User_Lets_NoRecordException_Through()
    {
        // Arrange
        A.CallTo(() => _confirmUserCommandHandler.UpdateItemAsync(A<ConfirmUserCommand>._, A<string>._, A<CancellationToken>._))
            .ThrowsAsync(NoRecordException.ForIdentifier("user-id"));

        var handler = CreateHandler();

        // Act & Assert
        await Assert.ThrowsAsync<NoRecordException>(() =>
            handler.HandleEventAsync(new CodeVerifiedEvent(MessageAction.WelcomeUser, "user-id"), TestContext.Current.CancellationToken)
        );
    }

    private CodeVerifiedEventHandler CreateHandler() => new(_confirmUserCommandHandler, _verifyPasswordResetCommandHandler);
}
