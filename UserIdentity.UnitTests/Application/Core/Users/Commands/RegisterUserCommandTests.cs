using System.ComponentModel.DataAnnotations;
using System.Linq;

using UserIdentity.Application.Core.Users.Commands;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class RegisterUserCommandTests
{
  [Fact]
  public void Validate_Without_Phone_Number_Or_Email_Fails_For_Both_Members()
  {
    // Arrange
    var command = CreateCommand(phoneNumber: null, userEmail: " ");

    // Act
    var results = command.Validate(new ValidationContext(command)).ToList();

    // Assert
    var result = Assert.Single(results);
    Assert.Equal("Either PhoneNumber or UserEmail must be provided.", result.ErrorMessage);
    Assert.Equal([nameof(RegisterUserCommand.PhoneNumber), nameof(RegisterUserCommand.UserEmail)], result.MemberNames);
  }

  [Theory]
  [InlineData("0712345678", null)]
  [InlineData(null, "test.user@domain.com")]
  public void Validate_With_Phone_Number_Or_Email_Passes(string? phoneNumber, string? userEmail)
  {
    // Arrange
    var command = CreateCommand(phoneNumber, userEmail);

    // Act
    var results = command.Validate(new ValidationContext(command));

    // Assert
    Assert.Empty(results);
  }

  private static RegisterUserCommand CreateCommand(string? phoneNumber, string? userEmail) => new()
  {
    FirstName = "Test",
    UserName = "test.user",
    UserPassword = "Password123",
    PhoneNumber = phoneNumber,
    UserEmail = userEmail
  };
}
