using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

using PolyzenKit.Infrastructure.Configurations;

using UserIdentity.Application.Core.Users.Shared;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Shared;

public class PasswordResetSettingsTests
{
  [Fact]
  public void ValidForSeconds_Below_One_Fails_Validation()
  {
    // Arrange
    var configuration = BuildConfiguration(validForSeconds: "0", maxRequestsPerDay: "5");

    // Act & Assert
    var exception = Assert.Throws<OptionsValidationException>(configuration.GetRequiredSetting<PasswordResetSettings>);
    Assert.Contains(exception.Failures, failure => failure.StartsWith("ValidForSeconds:", StringComparison.Ordinal));
  }

  [Fact]
  public void MaxRequestsPerDay_Below_One_Fails_Validation()
  {
    // Arrange
    var configuration = BuildConfiguration(validForSeconds: "900", maxRequestsPerDay: "0");

    // Act & Assert
    var exception = Assert.Throws<OptionsValidationException>(configuration.GetRequiredSetting<PasswordResetSettings>);
    Assert.Contains(exception.Failures, failure => failure.StartsWith("MaxRequestsPerDay:", StringComparison.Ordinal));
  }

  private static IConfiguration BuildConfiguration(string validForSeconds, string maxRequestsPerDay) =>
    new ConfigurationBuilder()
      .AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["PasswordResetSettings:ValidForSeconds"] = validForSeconds,
        ["PasswordResetSettings:MaxRequestsPerDay"] = maxRequestsPerDay
      })
      .Build();
}
