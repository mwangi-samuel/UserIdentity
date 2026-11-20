using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core.Messages.Events;
using UserIdentity.Common.Enums;
using UserIdentity.Domain.PasswordResets;
using UserIdentity.Domain.Users;

namespace UserIdentity.Application.Core.Users.Shared;

public static class UserMessages
{
    public const string UserRegistered = "user.registered";

    public const string PasswordResetRequested = "password-reset.requested";

    public const string LoginCodeRequested = "login-code.requested";

    public const string PasswordChanged = "password.changed";

    public const string SignUpAttempted = "sign-up.attempted";

    public const string TwoFactorChanged = "two-factor.changed";

    public const string ContactChanged = "contact.changed";

    public const string WelcomeUserAction = "WelcomeUser";

    public const string PasswordResetAction = "PasswordReset";

    public const string LoginCodeAction = "LoginCode";

    public const string PasswordChangedAction = "PasswordChanged";

    public const string SignUpAttemptedAction = "SignUpAttempted";

    public const string TwoFactorChangedAction = "TwoFactorChanged";

    public const string ContactChangedAction = "ContactChanged";

    public static MessageEvent Welcome(
        IdentityUser user,
        UserEntity userDetails,
        string appName,
        RequestContext requestContext,
        DateTime requestedAt
    ) =>
        MessageEventFactory.Create(
            user.Id,
            appName,
            WelcomeUserAction,
            Data(userDetails, requestContext) with
            {
                RequestedAt = requestedAt,
            },
            Recipient(user.Email, user.PhoneNumber, userDetails)
        );

    public static MessageEvent PasswordReset(
        PasswordResetEntity passwordReset,
        IdentityUser user,
        UserEntity userDetails,
        string appName,
        RequestContext requestContext,
        DateTime requestedAt
    ) =>
        MessageEventFactory.Create(
            passwordReset.Id.ToString(),
            appName,
            PasswordResetAction,
            Data(userDetails, requestContext) with
            {
                RequestedAt = requestedAt,
            },
            Recipient(user.Email, user.PhoneNumber, userDetails)
        ) with
        {
            ExpiresAt = passwordReset.Expires,
        };

    public static MessageEvent LoginCode(
        string correlationId,
        DateTime expiresAt,
        LoginCodePurpose purpose,
        string? email,
        string? phoneNumber,
        UserEntity userDetails,
        string appName,
        RequestContext requestContext,
        DateTime requestedAt
    ) =>
        MessageEventFactory.Create(
            correlationId,
            appName,
            LoginCodeAction,
            Data(userDetails, requestContext) with
            {
                Purpose = purpose,
                RequestedAt = requestedAt,
            },
            Recipient(email, phoneNumber, userDetails)
        ) with
        {
            ExpiresAt = expiresAt,
        };

    public static MessageEvent PasswordChange(
        IdentityUser user,
        UserEntity userDetails,
        string appName,
        RequestContext requestContext,
        DateTime changedAt
    ) =>
        MessageEventFactory.Create(
            NewCorrelationId(),
            appName,
            PasswordChangedAction,
            Data(userDetails, requestContext) with
            {
                ChangedAt = changedAt,
            },
            Recipient(user.Email, user.PhoneNumber, userDetails)
        );

    public static MessageEvent SignUpAttempt(
        IdentityUser user,
        UserEntity userDetails,
        string appName,
        RequestContext requestContext,
        DateTime attemptedAt
    ) =>
        MessageEventFactory.Create(
            NewCorrelationId(),
            appName,
            SignUpAttemptedAction,
            Data(userDetails, requestContext) with
            {
                AttemptedAt = attemptedAt,
            },
            Recipient(user.Email, user.PhoneNumber, userDetails)
        );

    public static MessageEvent TwoFactorChange(
        IdentityUser user,
        UserEntity userDetails,
        string appName,
        TwoFactorChange change,
        TwoFactorMethod? method,
        RequestContext requestContext,
        DateTime changedAt
    ) =>
        MessageEventFactory.Create(
            NewCorrelationId(),
            appName,
            TwoFactorChangedAction,
            Data(userDetails, requestContext) with
            {
                Change = change.ToString(),
                Method = method,
                ChangedAt = changedAt,
            },
            Recipient(user.Email, user.PhoneNumber, userDetails)
        );

    public static MessageEvent ContactChange(
        string? email,
        string? phoneNumber,
        UserEntity userDetails,
        string appName,
        ContactChange change,
        RequestContext requestContext,
        DateTime changedAt
    ) =>
        MessageEventFactory.Create(
            NewCorrelationId(),
            appName,
            ContactChangedAction,
            Data(userDetails, requestContext) with
            {
                Change = change.ToString(),
                ChangedAt = changedAt,
            },
            Recipient(email, phoneNumber, userDetails)
        );

    private static string NewCorrelationId() => Guid.CreateVersion7().ToString();

    private static UserMessageData Data(UserEntity userDetails, RequestContext requestContext) =>
        new()
        {
            FirstName = NullIfBlank(userDetails.FirstName),
            IpAddress = requestContext.IpAddress,
            Device = requestContext.Device,
        };

    private static MessageRecipient Recipient(string? email, string? phoneNumber, UserEntity userDetails) =>
        new()
        {
            Email = NullIfBlank(email),
            Phone = NullIfBlank(phoneNumber),
            Name = NullIfBlank($"{userDetails.FirstName} {userDetails.LastName}".Trim()),
        };

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
