using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

using PolyzenKit.Infrastructure.Configurations;

using UserIdentity.Application.Core.Users.Shared;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Shared;

public class CleanupSettingsTests
{
  [Fact]
  public void IntervalInMinutes_Below_One_Fails_Validation()
  {
    // Arrange
    var configuration = BuildConfiguration(intervalInMinutes: "0", unconfirmedUserLifetimeInDays: "30");

    // Act & Assert
    var exception = Assert.Throws<OptionsValidationException>(configuration.GetRequiredSetting<CleanupSettings>);
    Assert.Contains(exception.Failures, failure => failure.StartsWith("IntervalInMinutes:", StringComparison.Ordinal));
  }

  [Fact]
  public void UnconfirmedUserLifetimeInDays_Below_One_Fails_Validation()
  {
    // Arrange
    var configuration = BuildConfiguration(intervalInMinutes: "60", unconfirmedUserLifetimeInDays: "0");

    // Act & Assert
    var exception = Assert.Throws<OptionsValidationException>(configuration.GetRequiredSetting<CleanupSettings>);
    Assert.Contains(
      exception.Failures,
      failure => failure.StartsWith("UnconfirmedUserLifetimeInDays:", StringComparison.Ordinal));
  }

  private static IConfiguration BuildConfiguration(string intervalInMinutes, string unconfirmedUserLifetimeInDays) =>
    new ConfigurationBuilder()
      .AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["CleanupSettings:IntervalInMinutes"] = intervalInMinutes,
        ["CleanupSettings:UnconfirmedUserLifetimeInDays"] = unconfirmedUserLifetimeInDays
      })
      .Build();
}
