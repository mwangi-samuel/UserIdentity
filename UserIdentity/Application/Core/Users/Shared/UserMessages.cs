using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core.Messages.Events;
using UserIdentity.Domain.Users;

namespace UserIdentity.Application.Core.Users.Shared;

public static class UserMessages
{
    public const string UserRegistered = "user.registered";

    public const string PasswordResetRequested = "password-reset.requested";

    public const string CallbackRequested = "callback.requested";

    public const string WelcomeUserAction = "WelcomeUser";

    public const string PasswordResetAction = "PasswordReset";

    public static MessageEvent Welcome(IdentityUser user, UserEntity userDetails, string appName) =>
        MessageEventFactory.Create(user.Id, appName, WelcomeUserAction, Data(user, userDetails), Recipient(user, userDetails));

    public static MessageEvent PasswordReset(string resetRequestId, IdentityUser user, UserEntity userDetails, string appName) =>
        MessageEventFactory.Create(resetRequestId, appName, PasswordResetAction, Data(user, userDetails), Recipient(user, userDetails));

    private static UserMessageData Data(IdentityUser user, UserEntity userDetails) =>
        new()
        {
            UserId = user.Id,
            UserName = NullIfBlank(user.UserName),
            FirstName = NullIfBlank(userDetails.FirstName),
            LastName = NullIfBlank(userDetails.LastName),
        };

    private static MessageRecipient Recipient(IdentityUser user, UserEntity userDetails) =>
        new()
        {
            Email = NullIfBlank(user.Email),
            Phone = NullIfBlank(user.PhoneNumber),
            Name = NullIfBlank($"{userDetails.FirstName} {userDetails.LastName}".Trim()),
        };

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
