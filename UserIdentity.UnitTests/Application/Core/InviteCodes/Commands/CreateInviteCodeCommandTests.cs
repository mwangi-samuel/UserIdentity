using System.ComponentModel.DataAnnotations;
using UserIdentity.Application.Core.InviteCodes.Commands;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.InviteCodes.Commands;

public class CreateInviteCodeCommandTests
{
    [Fact]
    public void A_Valid_Command_Passes()
    {
        // Arrange
        var command = CreateCommand();

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
        var command = CreateCommand() with
        {
            UserEmail = userEmail,
        };

        // Act
        var results = Validate(command);

        // Assert
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(CreateInviteCodeCommand.UserEmail)));
    }

    [Fact]
    public void An_Email_Longer_Than_The_Column_Fails()
    {
        // Arrange
        var command = CreateCommand() with
        {
            UserEmail = $"{new string('a', 250)}@example.com",
        };

        // Act
        var results = Validate(command);

        // Assert
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(CreateInviteCodeCommand.UserEmail)));
    }

    [Fact]
    public void An_Invite_Code_Shorter_Than_Ten_Characters_Fails()
    {
        // Arrange
        var command = CreateCommand() with
        {
            InviteCode = "ABCDEFGHI",
        };

        // Act
        var results = Validate(command);

        // Assert
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(CreateInviteCodeCommand.InviteCode)));
    }

    [Fact]
    public void A_Role_Longer_Than_The_Column_Fails()
    {
        // Arrange
        var command = CreateCommand() with
        {
            Role = $"testapp:{new string('r', 250)}",
        };

        // Act
        var results = Validate(command);

        // Assert
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(CreateInviteCodeCommand.Role)));
    }

    private static CreateInviteCodeCommand CreateCommand() =>
        new()
        {
            UserEmail = "owner@example.com",
            InviteCode = "ABCDEFGHJK",
            Role = "testapp:owner",
        };

    private static List<ValidationResult> Validate(CreateInviteCodeCommand command)
    {
        List<ValidationResult> results = [];
        Validator.TryValidateObject(command, new ValidationContext(command), results, validateAllProperties: true);
        return results;
    }
}
