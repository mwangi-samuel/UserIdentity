using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Events;
using PolyzenKit.Infrastructure.Messaging.Receiving;
using UserIdentity.Application.Core.Users.Events;
using UserIdentity.Application.Core.Users.Shared;

namespace UserIdentity.Infrastructure.Messaging;

[MessageSubject("useridentity.callback.requested")]
public class CallbackRequestedMessageHandler(IEventHandler<UserRegisteredEvent> userRegisteredEventHandler) : IMessageHandler
{
    private readonly IEventHandler<UserRegisteredEvent> _userRegisteredEventHandler = userRegisteredEventHandler;

    public Task HandleAsync(MessageEvent message, CancellationToken cancellationToken = default)
    {
        var data = message.GetData<UserMessageData>();

        var userRegisteredEvent = new UserRegisteredEvent
        {
            AppName = message.RegisteredApp,
            UserId = data?.UserId ?? throw new InvalidDataException($"Callback message {message.CorrelationId} has no user id"),
            FirstName = data.FirstName,
            LastName = data.LastName,
            UserName = data.UserName,
            PhoneNumber = message.Recipient?.Phone,
            UserEmail = message.Recipient?.Email,
        };

        return _userRegisteredEventHandler.HandleEventAsync(userRegisteredEvent, cancellationToken);
    }
}
