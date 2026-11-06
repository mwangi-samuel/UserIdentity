using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using PolyzenKit.Infrastructure.Configurations;
using UserIdentity.Application.Core.Tokens.Shared;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Tokens.Shared;

public class RefreshTokenSettingsTests
{
    [Fact]
    public void ValidForSeconds_Below_One_Fails_Validation()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["RefreshTokenSettings:ValidForSeconds"] = "0" })
            .Build();

        // Act & Assert
        var exception = Assert.Throws<OptionsValidationException>(configuration.GetRequiredSetting<RefreshTokenSettings>);
        Assert.Contains(exception.Failures, failure => failure.StartsWith("ValidForSeconds:", StringComparison.Ordinal));
    }
}
