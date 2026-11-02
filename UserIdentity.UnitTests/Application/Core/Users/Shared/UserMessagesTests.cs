using System.Collections.Generic;

using Microsoft.AspNetCore.Identity;

using PolyzenKit.Common.Enums;

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
    PhoneNumber = "0712345678"
  };

  private static readonly UserEntity _userDetails = new() { Id = "user-id", FirstName = "Test", LastName = "User" };

  [Fact]
  public void Welcome_Is_Correlated_By_The_User_Id_And_Carries_The_Users_Details()
  {
    // Act
    var message = UserMessages.Welcome(_user, _userDetails, "TestApp");

    // Assert
    Assert.Equal(MessageAction.WELCOME_USER, message.Action);
    Assert.Equal("user-id", message.CorrelationId);
    Assert.Equal("TestApp", message.RegisteredApp);
    AssertAttributes(message.Attributes);
  }

  [Fact]
  public void PasswordReset_Is_Correlated_By_The_Plain_Reset_Request_Id()
  {
    // Act
    var message = UserMessages.PasswordReset("reset-request-id", _user, _userDetails, "TestApp");

    // Assert
    Assert.Equal(MessageAction.PASSWORD_RESET, message.Action);
    Assert.Equal("reset-request-id", message.CorrelationId);
    Assert.Equal("TestApp", message.RegisteredApp);
    AssertAttributes(message.Attributes);
  }

  [Fact]
  public void Missing_Details_Are_Sent_As_Empty_Attributes()
  {
    // Arrange
    var user = new IdentityUser { Id = "user-id", UserName = "test.user" };
    var userDetails = new UserEntity { Id = "user-id", FirstName = "Test" };

    // Act
    var message = UserMessages.Welcome(user, userDetails, "TestApp");

    // Assert
    Assert.Equal(string.Empty, message.Attributes[MessageAttribute.RECIPIENT_EMAIL]);
    Assert.Equal(string.Empty, message.Attributes[MessageAttribute.RECIPIENT_PHONE]);
    Assert.Equal(string.Empty, message.Attributes[MessageAttribute.LAST_NAME]);
  }

  private static void AssertAttributes(IReadOnlyDictionary<MessageAttribute, string> attributes)
  {
    Assert.Equal("test.user@domain.com", attributes[MessageAttribute.RECIPIENT_EMAIL]);
    Assert.Equal("0712345678", attributes[MessageAttribute.RECIPIENT_PHONE]);
    Assert.Equal("Test", attributes[MessageAttribute.FIRST_NAME]);
    Assert.Equal("User", attributes[MessageAttribute.LAST_NAME]);
    Assert.Equal("test.user", attributes[MessageAttribute.USER_NAME]);
    Assert.Equal("user-id", attributes[MessageAttribute.USER_ID]);
  }
}
