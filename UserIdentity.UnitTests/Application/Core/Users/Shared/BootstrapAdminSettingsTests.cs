using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using PolyzenKit.Infrastructure.Configurations;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Common.Errors;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Shared;

public class BootstrapAdminSettingsTests
{
    [Fact]
    public void Empty_Settings_Pass_Validation_And_Are_Disabled()
    {
        // Arrange
        var configuration = BuildConfiguration(CompleteSettings().ToDictionary(setting => setting.Key, _ => (string?)string.Empty));

        // Act
        var settings = configuration.GetRequiredSetting<BootstrapAdminSettings>();

        // Assert
        Assert.False(settings.IsEnabled);
    }

    [Fact]
    public void Complete_Settings_Pass_Validation_And_Are_Enabled()
    {
        // Arrange
        var configuration = BuildConfiguration(CompleteSettings());

        // Act
        var settings = configuration.GetRequiredSetting<BootstrapAdminSettings>();

        // Assert
        Assert.True(settings.IsEnabled);
    }

    [Theory]
    [InlineData("AppName")]
    [InlineData("AppBaseUrl")]
    [InlineData("UserName")]
    [InlineData("FirstName")]
    [InlineData("LastName")]
    [InlineData("Email")]
    [InlineData("Password")]
    public void A_Missing_Setting_Fails_Validation_Once_Any_Is_Set(string missingSetting)
    {
        // Arrange
        var values = CompleteSettings();
        values[missingSetting] = string.Empty;
        var configuration = BuildConfiguration(values);

        // Act & Assert
        var exception = Assert.Throws<OptionsValidationException>(configuration.GetRequiredSetting<BootstrapAdminSettings>);
        Assert.Equal([$"{missingSetting}: {ErrorMessages.Users.BootstrapAdminIncomplete([missingSetting])}"], exception.Failures);
    }

    [Fact]
    public void A_Missing_Phone_Number_Passes_Validation_And_Stays_Enabled()
    {
        // Arrange
        var values = CompleteSettings();
        values["PhoneNumber"] = string.Empty;
        var configuration = BuildConfiguration(values);

        // Act
        var settings = configuration.GetRequiredSetting<BootstrapAdminSettings>();

        // Assert
        Assert.True(settings.IsEnabled);
    }

    [Fact]
    public void A_Phone_Number_Alone_Passes_Validation_And_Is_Disabled()
    {
        // Arrange
        var values = CompleteSettings().ToDictionary(setting => setting.Key, _ => (string?)string.Empty);
        values["PhoneNumber"] = "+254700000001";
        var configuration = BuildConfiguration(values);

        // Act
        var settings = configuration.GetRequiredSetting<BootstrapAdminSettings>();

        // Assert
        Assert.False(settings.IsEnabled);
    }

    [Fact]
    public void An_Invalid_Email_Fails_Validation()
    {
        // Arrange
        var values = CompleteSettings();
        values["Email"] = "not-an-email";
        var configuration = BuildConfiguration(values);

        // Act & Assert
        var exception = Assert.Throws<OptionsValidationException>(configuration.GetRequiredSetting<BootstrapAdminSettings>);
        Assert.Contains(exception.Failures, failure => failure.StartsWith("Email:", StringComparison.Ordinal));
    }

    private static Dictionary<string, string?> CompleteSettings() =>
        new()
        {
            ["AppName"] = "BootstrapApp",
            ["AppBaseUrl"] = "https://bootstrap.test",
            ["UserName"] = "bootstrap.admin",
            ["FirstName"] = "Boot",
            ["LastName"] = "Strap",
            ["Email"] = "bootstrap.admin@example.com",
            ["PhoneNumber"] = "+254700000001",
            ["Password"] = "Adm1n@P@ss",
        };

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(value => $"BootstrapAdminSettings:{value.Key}", value => value.Value))
            .Build();
}
