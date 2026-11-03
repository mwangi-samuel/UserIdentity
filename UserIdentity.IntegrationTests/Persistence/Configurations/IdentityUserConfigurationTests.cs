using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using PolyzenKit.Common.Exceptions;

using UserIdentity.IntegrationTests.TestUtils;

using Xunit;

namespace UserIdentity.IntegrationTests.Persistence.Configurations;

public class IdentityUserConfigurationTests(
    TestingWebAppFactory testingWebAppFactory,
    ITestOutputHelper outputHelper
    ) : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
  private readonly TestingWebAppFactory _testingWebAppFactory = testingWebAppFactory;

  [Fact]
  public async Task A_Second_Account_With_An_Email_That_Differs_Only_In_Case_Is_Refused_As_Existing()
  {
    // Arrange
    TestDbHelper.CreateIdentityUser();

    using var scope = _testingWebAppFactory.Services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    var secondUser = new IdentityUser { UserName = "second.user", Email = UserSettingHelper.UserEmail.ToUpperInvariant() };

    // Act & Assert
    await Assert.ThrowsAsync<RecordExistsException>(() => userManager.CreateAsync(secondUser, UserSettingHelper.UserPassword));
  }

  [Fact]
  public async Task Accounts_Without_An_Email_Can_Share_The_Missing_Email()
  {
    // Arrange
    using var scope = _testingWebAppFactory.Services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

    // Act
    var first = await userManager.CreateAsync(new IdentityUser { UserName = "phone.only.one", PhoneNumber = "0711111111" }, UserSettingHelper.UserPassword);
    var second = await userManager.CreateAsync(new IdentityUser { UserName = "phone.only.two", PhoneNumber = "0722222222" }, UserSettingHelper.UserPassword);

    // Assert
    Assert.True(first.Succeeded);
    Assert.True(second.Succeeded);
  }
}
