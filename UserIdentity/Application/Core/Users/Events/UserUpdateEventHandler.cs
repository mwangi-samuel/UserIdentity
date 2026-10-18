using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Events;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Enums;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Domain.RegisteredApps;

using UserIdentity.Application.Enums;
using UserIdentity.Application.Interfaces;

namespace UserIdentity.Application.Core.Users.Events;

public record UserEventContent
{
  public string? UserIdentityId { get; set; }
  public string? FirstName { get; set; }
  public string? LastName { get; set; }
  public string? UserName { get; set; }
  public string? PhoneNumber { get; set; }
  public bool PhoneNumberConfirmed { get; set; }
  public string? UserEmail { get; set; }
  public bool EmailConfirmed { get; set; }

  public Dictionary<MessageAttribute, string> ToMessageAttributes() => new()
  {
    { MessageAttribute.RECIPIENT_EMAIL, UserEmail.EmptyIfNullOrWhiteSpace() },
    { MessageAttribute.RECIPIENT_PHONE, PhoneNumber.EmptyIfNullOrWhiteSpace() },
    { MessageAttribute.FIRST_NAME, FirstName.EmptyIfNullOrWhiteSpace() },
    { MessageAttribute.LAST_NAME, LastName.EmptyIfNullOrWhiteSpace() },
    { MessageAttribute.USER_NAME, UserName.EmptyIfNullOrWhiteSpace() },
    { MessageAttribute.USER_ID, UserIdentityId.EmptyIfNullOrWhiteSpace() }
  };
}

public record UserUpdateEvent : IBaseEvent
{
  public RequestSource RequestSource { get; set; }

  public CrudEvent EventType { get; set; }

  public UserEventContent UserContent { get; set; } = null!;

  public RegisteredAppEntity RegisteredApp { get; set; } = null!;
}

public class UserUpdateEventHandler(
  IAppCallbackService appCallbackService,
  IKafkaMessageProducer<string, MessageEvent> kafkaMessageProducer
  ) : IEventHandler<UserUpdateEvent>
{
  private readonly IAppCallbackService _appCallbackService = appCallbackService;
  private readonly IKafkaMessageProducer<string, MessageEvent> _kafkaMessageProducer = kafkaMessageProducer;

  private const string _userUpdateEventTopicKey = "UserUpdated";

  public async Task HandleEventAsync(UserUpdateEvent eventItem, CancellationToken cancellationToken = default)
  {
    if (eventItem.RequestSource == RequestSource.UI && eventItem.RegisteredApp.CallbackUrl != null)
      await _appCallbackService.SendCallbackRequestAsync(eventItem, cancellationToken);

    if (eventItem.EventType != CrudEvent.CREATE)
      return;

    var messageEvent = MessageEventFactory.Create(
      eventItem.UserContent.UserIdentityId,
      eventItem.RegisteredApp.AppName,
      eventItem.UserContent.ToMessageAttributes(),
      MessageAction.WELCOME_USER
    );

    await _kafkaMessageProducer.ProduceAsync(messageEvent.CorrelationId, messageEvent, _userUpdateEventTopicKey, cancellationToken);

  }
}
