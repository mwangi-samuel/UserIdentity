using PolyzenKit.Domain.Entities.RegisteredApps;
using UserIdentity.Domain.UserRegisteredApps;
using UserIdentity.Persistence;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using Xunit;

namespace UserIdentity.UnitTests.Persistence.Repositories.UserRegisteredApps;

public class UserRegisteredAppRepositoryTests
{
    [Theory]
    [InlineData("TestApp", true)]
    [InlineData("OtherApp", false)]
    [InlineData("DeletedApp", false)]
    public async Task IsUserRegisteredForAppAsync_Is_True_Only_For_The_Users_Active_App(string appName, bool expected)
    {
        // Arrange
        var context = AppDbContextTestFactory.GetAppDbContext();
        await SeedAsync(context);

        // Act
        var result = await new UserRegisteredAppRepository(context).IsUserRegisteredForAppAsync(
            "user-id",
            appName,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task IsUserRegisteredForAppAsync_For_Another_User_Is_False()
    {
        // Arrange
        var context = AppDbContextTestFactory.GetAppDbContext();
        await SeedAsync(context);

        // Act
        var result = await new UserRegisteredAppRepository(context).IsUserRegisteredForAppAsync(
            "other-user-id",
            "TestApp",
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result);
    }

    private static async Task SeedAsync(AppDbContext context)
    {
        context.RegisteredApp.AddRange(
            new RegisteredAppEntity
            {
                Id = 1,
                AppName = "TestApp",
                AppSecretKey = "secret-key",
                BaseUrl = "https://test.test",
            },
            new RegisteredAppEntity
            {
                Id = 2,
                AppName = "OtherApp",
                AppSecretKey = "secret-key",
                BaseUrl = "https://other.test",
            },
            new RegisteredAppEntity
            {
                Id = 3,
                AppName = "DeletedApp",
                AppSecretKey = "secret-key",
                BaseUrl = "https://deleted.test",
                IsDeleted = true,
            }
        );

        context.UserRegisteredApp.AddRange(
            new UserRegisteredAppEntity
            {
                Id = "user-id-1",
                UserId = "user-id",
                AppId = 1,
            },
            new UserRegisteredAppEntity
            {
                Id = "user-id-3",
                UserId = "user-id",
                AppId = 3,
            }
        );

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
