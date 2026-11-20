using System.ComponentModel.DataAnnotations;
using UserIdentity.Application.Core.SignInSettings.Commands;
using UserIdentity.Common.Errors;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.SignInSettings.Commands;

public class UpdateSignInSettingsCommandTests
{
    [Theory]
    [InlineData("member")]
    [InlineData("shop-owner")]
    [InlineData(null)]
    public void A_Role_Name_Without_Spaces_Or_Colons_Is_Accepted(string? defaultRole)
    {
        // Arrange
        var command = new UpdateSignInSettingsCommand { DefaultRole = defaultRole };

        // Act & Assert
        Assert.Empty(Validate(command));
    }

    [Theory]
    [InlineData("testapp:member")]
    [InlineData("shop owner")]
    [InlineData("")]
    public void A_Default_Role_With_A_Prefix_Or_Spaces_Is_Refused(string defaultRole)
    {
        // Arrange
        var command = new UpdateSignInSettingsCommand { DefaultRole = defaultRole };

        // Act & Assert
        Assert.NotEmpty(Validate(command));
    }

    [Fact]
    public void A_Default_Role_Longer_Than_The_Role_Name_Allows_After_The_App_Prefix_Is_Refused()
    {
        // Arrange
        var command = new UpdateSignInSettingsCommand { DefaultRole = new string('a', 156) };

        // Act & Assert
        Assert.NotEmpty(Validate(command));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Requiring_Two_Step_Sign_In_With_No_Method_Allowed_Is_Refused(bool requireTwoFactor, bool requireTwoFactorForAdmins)
    {
        // Arrange
        var command = new UpdateSignInSettingsCommand
        {
            RequireTwoFactor = requireTwoFactor,
            RequireTwoFactorForAdmins = requireTwoFactorForAdmins,
        };

        // Act
        var results = Validate(command);

        // Assert
        var result = Assert.Single(results);
        Assert.Equal(ErrorMessages.SignInSettings.TwoFactorWithoutMethod, result.ErrorMessage);
    }

    [Fact]
    public void Requiring_Two_Step_Sign_In_With_A_Method_Allowed_Is_Accepted()
    {
        // Arrange
        var command = new UpdateSignInSettingsCommand { RequireTwoFactor = true, AllowSmsTwoFactor = true };

        // Act & Assert
        Assert.Empty(Validate(command));
    }

    private static List<ValidationResult> Validate(UpdateSignInSettingsCommand command)
    {
        List<ValidationResult> results = [];
        Validator.TryValidateObject(command, new ValidationContext(command), results, validateAllProperties: true);

        return results;
    }
}
