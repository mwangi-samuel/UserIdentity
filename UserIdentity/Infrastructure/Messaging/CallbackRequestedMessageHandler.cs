using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Events;
using PolyzenKit.Common.Enums;
using PolyzenKit.Infrastructure.Messaging.Receiving;

using UserIdentity.Application.Core.Users.Events;

namespace UserIdentity.Infrastructure.Messaging;

[MessageSubject("useridentity.callback.requested")]
public class CallbackRequestedMessageHandler(
  IEventHandler<UserRegisteredEvent> userRegisteredEventHandler
  ) : IMessageHandler
{
  private readonly IEventHandler<UserRegisteredEvent> _userRegisteredEventHandler = userRegisteredEventHandler;

  public Task HandleAsync(MessageEvent message, CancellationToken cancellationToken = default)
  {
    var userRegisteredEvent = new UserRegisteredEvent
    {
      AppName = message.RegisteredApp,
      UserId = Attribute(message, MessageAttributeKey.UserId)
        ?? throw new InvalidDataException($"Callback message {message.CorrelationId} has no user id"),
      FirstName = Attribute(message, MessageAttributeKey.FirstName),
      LastName = Attribute(message, MessageAttributeKey.LastName),
      UserName = Attribute(message, MessageAttributeKey.UserName),
      PhoneNumber = Attribute(message, MessageAttributeKey.RecipientPhone),
      UserEmail = Attribute(message, MessageAttributeKey.RecipientEmail)
    };

    return _userRegisteredEventHandler.HandleEventAsync(userRegisteredEvent, cancellationToken);
  }

  // Missing values are sent as empty strings
  private static string? Attribute(MessageEvent message, MessageAttributeKey key) =>
    message.Attributes.TryGetValue(key, out var value) && value.Length > 0 ? value : null;
}
