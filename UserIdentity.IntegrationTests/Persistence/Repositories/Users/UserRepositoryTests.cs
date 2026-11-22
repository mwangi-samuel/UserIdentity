using Microsoft.Extensions.DependencyInjection;
using PolyzenKit.Common.Paging;
using UserIdentity.Domain.Users;
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
    public async Task SearchUserDtosByAppIdAsync_Matches_Names_Email_And_Phone_Ignoring_Case_Within_The_App()
    {
        // Arrange
        TestDbHelper.CreateIdentityUser();
        TestDbHelper.CreateUserEntity();
        TestDbHelper.CreateUserOfApp(TestingWebAppFactory.OtherAppName);

        var repository = ServiceProvider.GetRequiredService<IUserRepository>();
        var appId = TestDbHelper.GetRegisteredAppEntityByAppName(RegisteredApp.AppName).Id;

        // Act
        var byFirstName = await SearchAsync(repository, appId, "TEST");
        var byLastName = await SearchAsync(repository, appId, "usE");
        var byEmail = await SearchAsync(repository, appId, "@DOMAIN.com");
        var byPhone = await SearchAsync(repository, appId, "234567");
        var byOtherAppsName = await SearchAsync(repository, appId, "other");
        var byWildcard = await SearchAsync(repository, appId, "test_user");

        // Assert
        Assert.Equal([UserSettingHelper.UserId], byFirstName.Items.Select(user => user.Id));
        Assert.Equal([UserSettingHelper.UserId], byLastName.Items.Select(user => user.Id));
        Assert.Equal([UserSettingHelper.UserId], byEmail.Items.Select(user => user.Id));
        Assert.Equal([UserSettingHelper.UserId], byPhone.Items.Select(user => user.Id));
        Assert.Empty(byOtherAppsName.Items);
        Assert.Empty(byWildcard.Items);
        Assert.True(byEmail.Items[0].EmailConfirmed);
    }

    [Fact]
    public async Task SearchUserDtosByAppIdAsync_Filters_By_Role_And_Confirmation_Before_Paging_Newest_First()
    {
        // Arrange
        TestDbHelper.CreateIdentityUser();
        TestDbHelper.CreateUserEntity();
        TestDbHelper.CreateIdentityRole(TestDbHelper.DefaultRole);
        TestDbHelper.CreateIdentityUserRole(TestDbHelper.DefaultRole, UserSettingHelper.UserId);

        var newerUserId = TestDbHelper.CreateUserOfApp(RegisteredApp.AppName);
        var repository = ServiceProvider.GetRequiredService<IUserRepository>();
        var appId = TestDbHelper.GetRegisteredAppEntityByAppName(RegisteredApp.AppName).Id;

        // Act
        var all = await repository.SearchUserDtosByAppIdAsync(appId, null, null, null, 1, 10, TestContext.Current.CancellationToken);
        var secondPage = await repository.SearchUserDtosByAppIdAsync(appId, null, null, null, 2, 1, TestContext.Current.CancellationToken);
        var byRole = await repository.SearchUserDtosByAppIdAsync(
            appId,
            null,
            TestDbHelper.DefaultRole.ToUpperInvariant(),
            null,
            1,
            10,
            TestContext.Current.CancellationToken
        );
        var unconfirmed = await repository.SearchUserDtosByAppIdAsync(
            appId,
            null,
            null,
            false,
            1,
            10,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([newerUserId, UserSettingHelper.UserId], all.Items.Select(user => user.Id));
        Assert.Equal(2, secondPage.TotalCount);
        Assert.Equal([UserSettingHelper.UserId], secondPage.Items.Select(user => user.Id));
        Assert.Equal([UserSettingHelper.UserId], byRole.Items.Select(user => user.Id));
        Assert.Equal([newerUserId], unconfirmed.Items.Select(user => user.Id));
        Assert.Equal(1, unconfirmed.TotalCount);
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

    private static Task<PagedList<UserDto>> SearchAsync(IUserRepository repository, int appId, string search) =>
        repository.SearchUserDtosByAppIdAsync(appId, search, null, null, 1, 10, TestContext.Current.CancellationToken);
}
