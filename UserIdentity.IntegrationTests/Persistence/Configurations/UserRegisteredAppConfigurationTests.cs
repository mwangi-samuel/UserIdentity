using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PolyzenKit.Common.Exceptions;
using UserIdentity.Domain.UserRegisteredApps;
using UserIdentity.IntegrationTests.TestUtils;
using UserIdentity.Persistence;
using Xunit;

namespace UserIdentity.IntegrationTests.Persistence.Configurations;

public class UserRegisteredAppConfigurationTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
    : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
    private readonly TestingWebAppFactory _testingWebAppFactory = testingWebAppFactory;

    [Fact]
    public async Task A_Second_Link_Between_A_User_And_The_Same_App_Is_Refused_As_Existing()
    {
        // Arrange
        TestDbHelper.CreateIdentityUser();
        TestDbHelper.CreateUserEntity();

        using var scope = _testingWebAppFactory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        context.UserRegisteredApp.Add(BuildLink(TestDbHelper.GetRegisteredAppEntityByAppName(RegisteredApp.AppName).Id));

        // Act & Assert
        await Assert.ThrowsAsync<RecordExistsException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_User_Can_Be_Linked_To_A_Second_App()
    {
        // Arrange
        TestDbHelper.CreateIdentityUser();
        TestDbHelper.CreateUserEntity();

        using var scope = _testingWebAppFactory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        context.UserRegisteredApp.Add(BuildLink(TestDbHelper.GetRegisteredAppEntityByAppName(TestingWebAppFactory.OtherAppName).Id));

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var links = await context
            .UserRegisteredApp.AsNoTracking()
            .CountAsync(link => link.UserId == UserSettingHelper.UserId, TestContext.Current.CancellationToken);

        Assert.Equal(2, links);
    }

    private static UserRegisteredAppEntity BuildLink(int appId) =>
        new()
        {
            UserId = UserSettingHelper.UserId,
            AppId = appId,
            CreatedBy = UserSettingHelper.UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedBy = UserSettingHelper.UserId,
            UpdatedAt = DateTime.UtcNow,
        };
}
