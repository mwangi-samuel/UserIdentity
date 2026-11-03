using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Events;
using PolyzenKit.Infrastructure.Messaging.Receiving;

using UserIdentity.Application.Core.Users.Events;

namespace UserIdentity.Infrastructure.Messaging;

[MessageSubject("messagemanager.user.updated")]
public class CodeVerifiedMessageHandler(IEventHandler<CodeVerifiedEvent> codeVerifiedEventHandler) : IMessageHandler
{
  private readonly IEventHandler<CodeVerifiedEvent> _codeVerifiedEventHandler = codeVerifiedEventHandler;

  public Task HandleAsync(MessageEvent message, CancellationToken cancellationToken = default) =>
    _codeVerifiedEventHandler.HandleEventAsync(
      new CodeVerifiedEvent(message.Action, message.CorrelationId),
      cancellationToken);
}
