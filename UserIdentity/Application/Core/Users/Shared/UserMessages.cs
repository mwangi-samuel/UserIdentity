using Microsoft.AspNetCore.Identity;

using PolyzenKit.Application.Core.Messages.Events;
using PolyzenKit.Common.Enums;
using PolyzenKit.Common.Utilities;

using UserIdentity.Domain.Users;

namespace UserIdentity.Application.Core.Users.Shared;

public static class UserMessages
{
  public const string UserUpdated = "user.updated";

  public const string CallbackRequested = "callback.requested";

  public static MessageEvent Welcome(IdentityUser user, UserEntity userDetails, string appName) =>
    MessageEventFactory.Create(user.Id, appName, Attributes(user, userDetails), MessageAction.WELCOME_USER);

  public static MessageEvent PasswordReset(string resetRequestId, IdentityUser user, UserEntity userDetails, string appName) =>
    MessageEventFactory.Create(resetRequestId, appName, Attributes(user, userDetails), MessageAction.PASSWORD_RESET);

  private static Dictionary<MessageAttribute, string> Attributes(IdentityUser user, UserEntity userDetails) => new()
  {
    { MessageAttribute.RECIPIENT_EMAIL, user.Email.EmptyIfNullOrWhiteSpace() },
    { MessageAttribute.RECIPIENT_PHONE, user.PhoneNumber.EmptyIfNullOrWhiteSpace() },
    { MessageAttribute.FIRST_NAME, userDetails.FirstName.EmptyIfNullOrWhiteSpace() },
    { MessageAttribute.LAST_NAME, userDetails.LastName.EmptyIfNullOrWhiteSpace() },
    { MessageAttribute.USER_NAME, user.UserName.EmptyIfNullOrWhiteSpace() },
    { MessageAttribute.USER_ID, user.Id.EmptyIfNullOrWhiteSpace() }
  };
}
