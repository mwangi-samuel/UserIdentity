using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core.Messages.Events;
using PolyzenKit.Common.Enums;
using PolyzenKit.Common.Extensions;
using UserIdentity.Domain.Users;

namespace UserIdentity.Application.Core.Users.Shared;

public static class UserMessages
{
    public const string UserRegistered = "user.registered";

    public const string PasswordResetRequested = "password-reset.requested";

    public const string CallbackRequested = "callback.requested";

    public static MessageEvent Welcome(IdentityUser user, UserEntity userDetails, string appName) =>
        MessageEventFactory.Create(user.Id, appName, Attributes(user, userDetails), MessageAction.WelcomeUser);

    public static MessageEvent PasswordReset(string resetRequestId, IdentityUser user, UserEntity userDetails, string appName) =>
        MessageEventFactory.Create(resetRequestId, appName, Attributes(user, userDetails), MessageAction.PasswordReset);

    private static Dictionary<MessageAttributeKey, string> Attributes(IdentityUser user, UserEntity userDetails) =>
        new()
        {
            { MessageAttributeKey.RecipientEmail, user.Email.EmptyIfNullOrWhiteSpace() },
            { MessageAttributeKey.RecipientPhone, user.PhoneNumber.EmptyIfNullOrWhiteSpace() },
            { MessageAttributeKey.FirstName, userDetails.FirstName.EmptyIfNullOrWhiteSpace() },
            { MessageAttributeKey.LastName, userDetails.LastName.EmptyIfNullOrWhiteSpace() },
            { MessageAttributeKey.UserName, user.UserName.EmptyIfNullOrWhiteSpace() },
            { MessageAttributeKey.UserId, user.Id.EmptyIfNullOrWhiteSpace() },
        };
}
