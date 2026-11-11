using Microsoft.Extensions.DependencyInjection;
using UserIdentity.IntegrationTests.TestUtils;
using UserIdentity.Persistence.Repositories.Users;
using Xunit;

namespace UserIdentity.IntegrationTests.Persistence.Repositories.Users;

public class UserRepositoryTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
    : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
    [Fact]
    public async Task GetUserDtoAsync_With_The_Apps_Name_In_Lower_Case_Reads_Its_User()
    {
        // Arrange
        TestDbHelper.CreateIdentityUser();
        TestDbHelper.CreateUserEntity();

        var repository = ServiceProvider.GetRequiredService<IUserRepository>();

        // Act
        var dto = await repository.GetUserDtoAsync(
            UserSettingHelper.UserId,
            RegisteredApp.AppName.ToLowerInvariant(),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(UserSettingHelper.UserId, dto?.Id);
    }

    [Fact]
    public async Task GetUserDtoAsync_For_Another_Apps_User_Returns_Null()
    {
        // Arrange
        TestDbHelper.CreateIdentityUser();
        TestDbHelper.CreateUserEntity();

        var repository = ServiceProvider.GetRequiredService<IUserRepository>();

        // Act
        var dto = await repository.GetUserDtoAsync(
            UserSettingHelper.UserId,
            TestingWebAppFactory.OtherAppName.ToLowerInvariant(),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(dto);
    }

    [Fact]
    public async Task GetUserDtoAsync_Without_An_App_Reads_Any_Apps_User()
    {
        // Arrange
        var otherAppUserId = TestDbHelper.CreateUserOfApp(TestingWebAppFactory.OtherAppName);

        var repository = ServiceProvider.GetRequiredService<IUserRepository>();

        // Act
        var dto = await repository.GetUserDtoAsync(otherAppUserId, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(otherAppUserId, dto?.Id);
    }
}
