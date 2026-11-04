using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using UserIdentity.IntegrationTests.TestUtils;
using UserIdentity.Persistence;

using Xunit;

namespace UserIdentity.IntegrationTests.Persistence.Configurations;

public class UserConfigurationTests(
  TestingWebAppFactory testingWebAppFactory,
  ITestOutputHelper outputHelper
  ) : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
  private readonly TestingWebAppFactory _testingWebAppFactory = testingWebAppFactory;

  [Fact]
  public async Task Deleting_An_Identity_User_Removes_Its_Details_App_Links_Refresh_Tokens_And_Reset_Requests()
  {
    // Arrange
    TestDbHelper.SeedDatabase();
    TestDbHelper.CreatePasswordResetEntity("reset-request-id");

    using var scope = _testingWebAppFactory.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var userId = UserSettingHelper.UserId;

    Assert.True(await context.AppUser.AnyAsync(details => details.Id == userId, TestContext.Current.CancellationToken));
    Assert.True(
      await context.UserRegisteredApp.AnyAsync(
        registration => registration.UserId == userId,
        TestContext.Current.CancellationToken));
    Assert.True(
      await context.RefreshToken.AnyAsync(token => token.UserId == userId, TestContext.Current.CancellationToken));
    Assert.True(
      await context.PasswordReset.AnyAsync(reset => reset.UserId == userId, TestContext.Current.CancellationToken));

    // Act
    await context.Users.Where(user => user.Id == userId).ExecuteDeleteAsync(TestContext.Current.CancellationToken);

    // Assert
    Assert.False(
      await context.AppUser.AnyAsync(details => details.Id == userId, TestContext.Current.CancellationToken));
    Assert.False(
      await context.UserRegisteredApp.AnyAsync(
        registration => registration.UserId == userId,
        TestContext.Current.CancellationToken));
    Assert.False(
      await context.RefreshToken.AnyAsync(token => token.UserId == userId, TestContext.Current.CancellationToken));
    Assert.False(
      await context.PasswordReset.AnyAsync(reset => reset.UserId == userId, TestContext.Current.CancellationToken));
  }
}
