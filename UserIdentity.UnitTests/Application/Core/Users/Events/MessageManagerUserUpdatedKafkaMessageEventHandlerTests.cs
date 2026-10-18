using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Events;
using PolyzenKit.Common.Enums;

using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Events;
using UserIdentity.Application.Core.Users.ViewModels;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Events;

public class MessageManagerUserUpdatedKafkaMessageEventHandlerTests
{
  private readonly IUpdateItemCommandHandler<ConfirmUserCommand, ConfirmUserViewModel> _confirmUserCommandHandler;
  private readonly IUpdateItemCommandHandler<VerifyPasswordResetCommand, VerifyPasswordResetViewModel> _verifyPasswordResetCommandHandler;

  public MessageManagerUserUpdatedKafkaMessageEventHandlerTests()
  {
    _confirmUserCommandHandler = A.Fake<IUpdateItemCommandHandler<ConfirmUserCommand, ConfirmUserViewModel>>();
    _verifyPasswordResetCommandHandler = A.Fake<IUpdateItemCommandHandler<VerifyPasswordResetCommand, VerifyPasswordResetViewModel>>();
  }

  [Fact]
  public async Task HandleEventAsync_WhenActionIsWelcomeUser_CallsConfirmUserCommandHandler()
  {
    var eventItem = GetEventItem(MessageAction.WELCOME_USER, "user-id");

    var handler = new MessageManagerUserUpdatedKafkaMessageEventHandler(_confirmUserCommandHandler, _verifyPasswordResetCommandHandler);

    await handler.HandleEventAsync(eventItem, TestContext.Current.CancellationToken);

    A.CallTo(() => _confirmUserCommandHandler.UpdateItemAsync(A<ConfirmUserCommand>.That.Matches(c => c.UserId == eventItem.MessageValue.CorrelationId), eventItem.MessageKey, A<CancellationToken>._))
    .MustHaveHappenedOnceExactly();
    A.CallTo(() => _verifyPasswordResetCommandHandler.UpdateItemAsync(A<VerifyPasswordResetCommand>._, A<string>._, A<CancellationToken>._))
      .MustNotHaveHappened();
  }

  [Fact]
  public async Task HandleEventAsync_WhenActionIsPasswordReset_CallsVerifyPasswordResetCommandHandler()
  {
    var eventItem = GetEventItem(MessageAction.PASSWORD_RESET, "reset-request-id");

    var handler = new MessageManagerUserUpdatedKafkaMessageEventHandler(_confirmUserCommandHandler, _verifyPasswordResetCommandHandler);

    await handler.HandleEventAsync(eventItem, TestContext.Current.CancellationToken);

    A.CallTo(() => _verifyPasswordResetCommandHandler.UpdateItemAsync(A<VerifyPasswordResetCommand>.That.Matches(c => c.ResetRequestId == eventItem.MessageValue.CorrelationId), eventItem.MessageKey, A<CancellationToken>._))
    .MustHaveHappenedOnceExactly();
    A.CallTo(() => _confirmUserCommandHandler.UpdateItemAsync(A<ConfirmUserCommand>._, A<string>._, A<CancellationToken>._))
      .MustNotHaveHappened();
  }

  [Fact]
  public async Task HandleEventAsync_WhenActionIsNotHandled_DoesNotCallAnyCommandHandler()
  {
    var eventItem = GetEventItem((MessageAction)999, "user-id");

    var handler = new MessageManagerUserUpdatedKafkaMessageEventHandler(_confirmUserCommandHandler, _verifyPasswordResetCommandHandler);

    await handler.HandleEventAsync(eventItem, TestContext.Current.CancellationToken);

    A.CallTo(() => _confirmUserCommandHandler.UpdateItemAsync(A<ConfirmUserCommand>._, A<string>._, A<CancellationToken>._))
      .MustNotHaveHappened();
    A.CallTo(() => _verifyPasswordResetCommandHandler.UpdateItemAsync(A<VerifyPasswordResetCommand>._, A<string>._, A<CancellationToken>._))
      .MustNotHaveHappened();
  }

  private static MessageManagerUserUpdatedKafkaMessageEvent GetEventItem(MessageAction action, string correlationId)
  {
    return new MessageManagerUserUpdatedKafkaMessageEvent
    {
      MessageKey = "message-key",
      MessageValue = new MessageEvent
      {
        Action = action,
        CorrelationId = correlationId,
        RegisteredApp = "TestApp",
        Attributes = new Dictionary<MessageAttribute, string>()
      }
    };
  }
}
