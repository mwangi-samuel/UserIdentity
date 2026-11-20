using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core.Messages.Events;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Common.Enums;
using UserIdentity.Domain.PasswordResets;
using UserIdentity.Domain.Users;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Shared;

public class UserMessagesTests
{
    private static readonly DateTime Now = new(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);

    private static readonly IdentityUser _user = new()
    {
        Id = "user-id",
        UserName = "test.user",
        Email = "test.user@domain.com",
        PhoneNumber = "0712345678",
    };

    private static readonly UserEntity _userDetails = new()
    {
        Id = "user-id",
        FirstName = "Test",
        LastName = "User",
    };

    private static readonly RequestContext _requestContext = RequestContext.Create("203.0.113.7", "Test Browser");

    private static readonly PasswordResetEntity _passwordReset = new()
    {
        Id = Guid.Parse("0196f1a2-0000-7000-8000-000000000001"),
        RequestId = "reset-request-hash",
        UserId = "user-id",
        AppId = 1,
        Expires = Now.AddMinutes(15),
    };

    [Fact]
    public void Welcome_Is_Correlated_By_The_User_Id_And_Carries_The_Request()
    {
        // Act
        var message = UserMessages.Welcome(_user, _userDetails, "TestApp", _requestContext, Now);

        // Assert
        Assert.Equal("WelcomeUser", message.Action);
        Assert.Equal("user-id", message.CorrelationId);
        Assert.Equal("TestApp", message.RegisteredApp);
        Assert.Null(message.ExpiresAt);
        AssertAccountRecipient(message);
        Assert.Equal(
            """{"firstName":"Test","requestedAt":"2026-11-18T12:00:00Z","ipAddress":"203.0.113.7","device":"Test Browser"}""",
            message.Data.GetRawText()
        );
    }

    [Fact]
    public void PasswordReset_Is_Correlated_By_The_Reset_Id_And_Expires_With_The_Reset()
    {
        // Act
        var message = UserMessages.PasswordReset(_passwordReset, _user, _userDetails, "TestApp", _requestContext, Now);

        // Assert
        Assert.Equal("PasswordReset", message.Action);
        Assert.Equal("0196f1a2-0000-7000-8000-000000000001", message.CorrelationId);
        Assert.Equal(_passwordReset.Expires, message.ExpiresAt);
        AssertAccountRecipient(message);
        Assert.Equal(Now, message.GetData<UserMessageData>()?.RequestedAt);
    }

    [Fact]
    public void LoginCode_Goes_Only_To_The_Given_Address_With_Its_Purpose_And_Expiry()
    {
        // Act
        var message = UserMessages.LoginCode(
            "ticket-id",
            Now.AddMinutes(10),
            LoginCodePurpose.SetUp,
            null,
            "+254700000123",
            _userDetails,
            "TestApp",
            _requestContext,
            Now
        );

        // Assert
        Assert.Equal("LoginCode", message.Action);
        Assert.Equal("ticket-id", message.CorrelationId);
        Assert.Equal(Now.AddMinutes(10), message.ExpiresAt);
        Assert.Equal(new MessageRecipient { Phone = "+254700000123", Name = "Test User" }, message.Recipient);
        Assert.Equal(
            """{"firstName":"Test","purpose":"SetUp","requestedAt":"2026-11-18T12:00:00Z","ipAddress":"203.0.113.7","device":"Test Browser"}""",
            message.Data.GetRawText()
        );
    }

    [Fact]
    public void PasswordChange_Has_Its_Own_Correlation_Id_And_Carries_When_It_Changed()
    {
        // Act
        var message = UserMessages.PasswordChange(_user, _userDetails, "TestApp", _requestContext, Now);

        // Assert
        Assert.Equal("PasswordChanged", message.Action);
        Assert.True(Guid.TryParse(message.CorrelationId, out _));
        Assert.Equal(Now, message.GetData<UserMessageData>()?.ChangedAt);
        Assert.Equal("203.0.113.7", message.GetData<UserMessageData>()?.IpAddress);
        AssertAccountRecipient(message);
    }

    [Fact]
    public void SignUpAttempt_Goes_To_The_Existing_Account_With_When_It_Was_Attempted()
    {
        // Act
        var message = UserMessages.SignUpAttempt(_user, _userDetails, "TestApp", _requestContext, Now);

        // Assert
        Assert.Equal("SignUpAttempted", message.Action);
        Assert.True(Guid.TryParse(message.CorrelationId, out _));
        Assert.Equal(Now, message.GetData<UserMessageData>()?.AttemptedAt);
        AssertAccountRecipient(message);
    }

    [Fact]
    public void TwoFactorChange_Carries_The_Change_And_The_Method()
    {
        // Act
        var message = UserMessages.TwoFactorChange(
            _user,
            _userDetails,
            "TestApp",
            TwoFactorChange.Reset,
            TwoFactorMethod.Authenticator,
            new RequestContext(),
            Now
        );

        // Assert
        Assert.Equal("TwoFactorChanged", message.Action);
        Assert.Equal(
            """{"firstName":"Test","change":"Reset","method":"Authenticator","changedAt":"2026-11-18T12:00:00Z"}""",
            message.Data.GetRawText()
        );
        AssertAccountRecipient(message);
    }

    [Fact]
    public void ContactChange_Goes_To_The_Given_Address()
    {
        // Act
        var message = UserMessages.ContactChange(
            "old@domain.com",
            null,
            _userDetails,
            "TestApp",
            ContactChange.Email,
            _requestContext,
            Now
        );

        // Assert
        Assert.Equal("ContactChanged", message.Action);
        Assert.Equal(new MessageRecipient { Email = "old@domain.com", Name = "Test User" }, message.Recipient);
        Assert.Equal("Email", message.GetData<UserMessageData>()?.Change);
    }

    [Fact]
    public void Messages_Carry_No_Locale_Or_Notify_Subject()
    {
        // Act
        var message = UserMessages.Welcome(_user, _userDetails, "TestApp", _requestContext, Now);

        // Assert
        Assert.Null(message.Locale);
        Assert.Null(message.NotifySubject);
    }

    [Theory]
    [InlineData(null, null, null, null)]
    [InlineData("", "", "", "")]
    [InlineData(" ", " ", " ", " ")]
    public void Blank_Details_Are_Left_Out(string? email, string? phoneNumber, string? firstName, string? lastName)
    {
        // Arrange
        var user = new IdentityUser
        {
            Id = "user-id",
            Email = email,
            PhoneNumber = phoneNumber,
        };
        var userDetails = new UserEntity
        {
            Id = "user-id",
            FirstName = firstName,
            LastName = lastName,
        };

        // Act
        var message = UserMessages.Welcome(user, userDetails, "TestApp", new RequestContext(), Now);

        // Assert
        Assert.Equal(new MessageRecipient(), message.Recipient);
        Assert.Equal("""{"requestedAt":"2026-11-18T12:00:00Z"}""", message.Data.GetRawText());
    }

    [Theory]
    [InlineData("Test", "User", "Test User")]
    [InlineData("Test", null, "Test")]
    [InlineData("Test", " ", "Test")]
    [InlineData(null, "User", "User")]
    [InlineData("", "User", "User")]
    public void Recipient_Name_Joins_The_First_And_Last_Names(string? firstName, string? lastName, string expectedName)
    {
        // Arrange
        var userDetails = new UserEntity
        {
            Id = "user-id",
            FirstName = firstName,
            LastName = lastName,
        };

        // Act
        var message = UserMessages.PasswordReset(_passwordReset, _user, userDetails, "TestApp", _requestContext, Now);

        // Assert
        Assert.Equal(expectedName, message.Recipient?.Name);
    }

    private static void AssertAccountRecipient(MessageEvent message) =>
        Assert.Equal(
            new MessageRecipient
            {
                Email = "test.user@domain.com",
                Phone = "0712345678",
                Name = "Test User",
            },
            message.Recipient
        );
}
