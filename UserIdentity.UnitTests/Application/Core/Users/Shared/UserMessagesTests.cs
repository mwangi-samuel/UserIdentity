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
    Assert.Equal(MessageAction.WelcomeUser, message.Action);
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
    Assert.Equal(MessageAction.PasswordReset, message.Action);
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
    Assert.Equal(string.Empty, message.Attributes[MessageAttributeKey.RecipientEmail]);
    Assert.Equal(string.Empty, message.Attributes[MessageAttributeKey.RecipientPhone]);
    Assert.Equal(string.Empty, message.Attributes[MessageAttributeKey.LastName]);
  }

  private static void AssertAttributes(IReadOnlyDictionary<MessageAttributeKey, string> attributes)
  {
    Assert.Equal("test.user@domain.com", attributes[MessageAttributeKey.RecipientEmail]);
    Assert.Equal("0712345678", attributes[MessageAttributeKey.RecipientPhone]);
    Assert.Equal("Test", attributes[MessageAttributeKey.FirstName]);
    Assert.Equal("User", attributes[MessageAttributeKey.LastName]);
    Assert.Equal("test.user", attributes[MessageAttributeKey.UserName]);
    Assert.Equal("user-id", attributes[MessageAttributeKey.UserId]);
  }
}
