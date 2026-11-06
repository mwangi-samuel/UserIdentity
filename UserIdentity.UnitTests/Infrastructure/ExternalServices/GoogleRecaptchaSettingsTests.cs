using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using PolyzenKit.Infrastructure.Configurations;
using UserIdentity.Infrastructure.ExternalServices;
using Xunit;

namespace UserIdentity.UnitTests.Infrastructure.ExternalServices;

public class GoogleRecaptchaSettingsTests
{
    [Fact]
    public void Enabled_Without_A_SiteKey_Fails_Validation()
    {
        // Arrange
        var configuration = BuildConfiguration(enabled: "true");

        // Act & Assert
        var exception = Assert.Throws<OptionsValidationException>(configuration.GetRequiredSetting<GoogleRecaptchaSettings>);
        Assert.Contains(exception.Failures, failure => failure.StartsWith("SiteKey:", StringComparison.Ordinal));
    }

    [Fact]
    public void Disabled_Without_A_SiteKey_Passes_Validation()
    {
        // Arrange
        var configuration = BuildConfiguration(enabled: "false");

        // Act
        var settings = configuration.GetRequiredSetting<GoogleRecaptchaSettings>();

        // Assert
        Assert.False(settings.Enabled);
        Assert.Null(settings.SiteKey);
    }

    private static IConfiguration BuildConfiguration(string enabled) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GoogleRecaptchaSettings:Enabled"] = enabled })
            .Build();
}
