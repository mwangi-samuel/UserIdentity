using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core.Messages.Events;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Domain.Users;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Shared;

public class UserMessagesTests
{
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

    [Fact]
    public void Welcome_Is_Correlated_By_The_User_Id_And_Carries_The_Users_Details()
    {
        // Act
        var message = UserMessages.Welcome(_user, _userDetails, "TestApp");

        // Assert
        Assert.Equal("WelcomeUser", message.Action);
        Assert.Equal("user-id", message.CorrelationId);
        Assert.Equal("TestApp", message.RegisteredApp);
        AssertUserDetails(message);
    }

    [Fact]
    public void PasswordReset_Is_Correlated_By_The_Plain_Reset_Request_Id()
    {
        // Act
        var message = UserMessages.PasswordReset("reset-request-id", _user, _userDetails, "TestApp");

        // Assert
        Assert.Equal("PasswordReset", message.Action);
        Assert.Equal("reset-request-id", message.CorrelationId);
        Assert.Equal("TestApp", message.RegisteredApp);
        AssertUserDetails(message);
    }

    [Fact]
    public void Messages_Carry_No_Locale_Or_Reply_Subject()
    {
        // Act
        var message = UserMessages.Welcome(_user, _userDetails, "TestApp");

        // Assert
        Assert.Null(message.Locale);
        Assert.Null(message.ReplySubject);
    }

    [Theory]
    [InlineData(null, null, null, null, null)]
    [InlineData("", "", "", "", "")]
    [InlineData(" ", " ", " ", " ", " ")]
    public void Blank_Details_Are_Left_Out(string? userName, string? email, string? phoneNumber, string? firstName, string? lastName)
    {
        // Arrange
        var user = new IdentityUser
        {
            Id = "user-id",
            UserName = userName,
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
        var message = UserMessages.Welcome(user, userDetails, "TestApp");

        // Assert
        Assert.Equal(new MessageRecipient(), message.Recipient);
        Assert.Equal("""{"userId":"user-id"}""", message.Data.GetRawText());
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
        var message = UserMessages.PasswordReset("reset-request-id", _user, userDetails, "TestApp");

        // Assert
        Assert.Equal(expectedName, message.Recipient?.Name);
    }

    private static void AssertUserDetails(MessageEvent message)
    {
        Assert.Equal(
            new MessageRecipient
            {
                Email = "test.user@domain.com",
                Phone = "0712345678",
                Name = "Test User",
            },
            message.Recipient
        );
        Assert.Equal("""{"userId":"user-id","userName":"test.user","firstName":"Test","lastName":"User"}""", message.Data.GetRawText());
    }
}
