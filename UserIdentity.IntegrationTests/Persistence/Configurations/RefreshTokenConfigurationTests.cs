using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using UserIdentity.IntegrationTests.TestUtils;
using UserIdentity.Persistence;

using Xunit;

namespace UserIdentity.IntegrationTests.Persistence.Configurations;

public class RefreshTokenConfigurationTests(
    TestingWebAppFactory testingWebAppFactory,
    ITestOutputHelper outputHelper
    ) : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
  private readonly TestingWebAppFactory _testingWebAppFactory = testingWebAppFactory;

  [Fact]
  public async Task Two_Exchanges_Of_One_Refresh_Token_Let_Only_The_First_Save()
  {
    // Arrange
    TestDbHelper.SeedDatabase();
    var refreshTokenId = TestDbHelper.GetRefreshTokenEntity(UserSettingHelper.UserId)!.Id;

    using var firstScope = _testingWebAppFactory.Services.CreateScope();
    using var secondScope = _testingWebAppFactory.Services.CreateScope();
    var firstContext = firstScope.ServiceProvider.GetRequiredService<AppDbContext>();
    var secondContext = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();

    var first = await firstContext.RefreshToken.SingleAsync(
      token => token.Id == refreshTokenId,
      TestContext.Current.CancellationToken);
    var second = await secondContext.RefreshToken.SingleAsync(
      token => token.Id == refreshTokenId,
      TestContext.Current.CancellationToken);

    first.Token = "first-exchange";
    first.Version = Guid.NewGuid();
    second.Token = "second-exchange";
    second.Version = Guid.NewGuid();

    await firstContext.SaveChangesAsync(TestContext.Current.CancellationToken);

    // Act & Assert
    await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondContext.SaveChangesAsync(
      TestContext.Current.CancellationToken));
  }
}
