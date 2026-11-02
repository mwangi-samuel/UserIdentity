using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

using UserIdentity.Application.Core.WaitLists.Commands;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.WaitLists.Commands;

public class CreateWaitListCommandTests
{
  [Fact]
  public void A_Valid_Email_Passes()
  {
    // Arrange
    var command = new CreateWaitListCommand { UserEmail = "shopper@example.com" };

    // Act
    var results = Validate(command);

    // Assert
    Assert.Empty(results);
  }

  [Theory]
  [InlineData("not-an-email")]
  [InlineData("")]
  public void An_Invalid_Email_Fails(string userEmail)
  {
    // Arrange
    var command = new CreateWaitListCommand { UserEmail = userEmail };

    // Act
    var results = Validate(command);

    // Assert
    Assert.Contains(results, result => result.MemberNames.Contains(nameof(CreateWaitListCommand.UserEmail)));
  }

  [Fact]
  public void An_Email_Longer_Than_The_Column_Fails()
  {
    // Arrange
    var command = new CreateWaitListCommand { UserEmail = $"{new string('a', 250)}@example.com" };

    // Act
    var results = Validate(command);

    // Assert
    Assert.Contains(results, result => result.MemberNames.Contains(nameof(CreateWaitListCommand.UserEmail)));
  }

  private static List<ValidationResult> Validate(CreateWaitListCommand command)
  {
    List<ValidationResult> results = [];
    Validator.TryValidateObject(command, new ValidationContext(command), results, validateAllProperties: true);
    return results;
  }
}
