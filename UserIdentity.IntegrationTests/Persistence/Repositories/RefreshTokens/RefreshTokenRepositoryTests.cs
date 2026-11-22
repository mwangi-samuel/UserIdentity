using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using UserIdentity.Domain.RefreshTokens;
using UserIdentity.IntegrationTests.TestUtils;
using UserIdentity.Persistence;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using Xunit;

namespace UserIdentity.IntegrationTests.Persistence.Repositories.RefreshTokens;

public class RefreshTokenRepositoryTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
    : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
    [Fact]
    public async Task DeleteEntityItemsAsync_Removes_All_The_Users_RefreshTokens()
    {
        // Arrange
        var otherUserId = CreateUsers();
        var testAppId = AppId(RegisteredApp.AppName);
        var otherAppId = AppId(TestingWebAppFactory.OtherAppName);

        await AddRefreshTokensAsync(
            BuildRefreshToken(UserSettingHelper.UserId, testAppId),
            BuildRefreshToken(UserSettingHelper.UserId, otherAppId),
            BuildRefreshToken(otherUserId, testAppId)
        );

        var repository = ServiceProvider.GetRequiredService<IRefreshTokenRepository>();

        // Act
        var deleted = await repository.DeleteEntityItemsAsync(
            UserSettingHelper.UserId,
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(2, deleted);
        Assert.Empty(TestDbHelper.GetRefreshTokenEntitiesWithNoTracking(UserSettingHelper.UserId));
        Assert.Single(TestDbHelper.GetRefreshTokenEntitiesWithNoTracking(otherUserId));
    }

    [Fact]
    public async Task DeleteEntityItemsAsync_With_An_App_Removes_Only_The_Users_Sessions_In_That_App()
    {
        // Arrange
        CreateUsers();
        var testAppId = AppId(RegisteredApp.AppName);
        var otherAppSession = BuildRefreshToken(UserSettingHelper.UserId, AppId(TestingWebAppFactory.OtherAppName));

        await AddRefreshTokensAsync(
            BuildRefreshToken(UserSettingHelper.UserId, testAppId),
            BuildRefreshToken(UserSettingHelper.UserId, testAppId),
            otherAppSession
        );

        var repository = ServiceProvider.GetRequiredService<IRefreshTokenRepository>();

        // Act
        var deleted = await repository.DeleteEntityItemsAsync(
            UserSettingHelper.UserId,
            testAppId,
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(2, deleted);
        Assert.Equal([otherAppSession.Id], TestDbHelper.GetRefreshTokenEntitiesWithNoTracking(UserSettingHelper.UserId).Select(t => t.Id));
    }

    [Fact]
    public async Task DeleteEntityItemsAsync_With_An_Id_Removes_Only_That_Session_Of_The_User_In_The_App()
    {
        // Arrange
        var otherUserId = CreateUsers();
        var testAppId = AppId(RegisteredApp.AppName);
        var ended = BuildRefreshToken(UserSettingHelper.UserId, testAppId);
        var kept = BuildRefreshToken(UserSettingHelper.UserId, testAppId);
        var otherUsersSession = BuildRefreshToken(otherUserId, testAppId);

        await AddRefreshTokensAsync(ended, kept, otherUsersSession);

        var repository = ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var deleted = await repository.DeleteEntityItemsAsync(
            UserSettingHelper.UserId,
            testAppId,
            ended.Id,
            cancellationToken: cancellationToken
        );
        var deletedInOtherApp = await repository.DeleteEntityItemsAsync(
            UserSettingHelper.UserId,
            AppId(TestingWebAppFactory.OtherAppName),
            kept.Id,
            cancellationToken: cancellationToken
        );
        var deletedForOtherUser = await repository.DeleteEntityItemsAsync(
            UserSettingHelper.UserId,
            testAppId,
            otherUsersSession.Id,
            cancellationToken: cancellationToken
        );

        // Assert
        Assert.Equal(1, deleted);
        Assert.Equal(0, deletedInOtherApp);
        Assert.Equal(0, deletedForOtherUser);
        Assert.Equal([kept.Id], TestDbHelper.GetRefreshTokenEntitiesWithNoTracking(UserSettingHelper.UserId).Select(t => t.Id));
        Assert.Single(TestDbHelper.GetRefreshTokenEntitiesWithNoTracking(otherUserId));
    }

    [Fact]
    public async Task DeleteEntityItemsAsync_Except_An_Id_Keeps_That_Session()
    {
        // Arrange
        CreateUsers();
        var testAppId = AppId(RegisteredApp.AppName);
        var current = BuildRefreshToken(UserSettingHelper.UserId, testAppId);

        await AddRefreshTokensAsync(
            BuildRefreshToken(UserSettingHelper.UserId, testAppId),
            current,
            BuildRefreshToken(UserSettingHelper.UserId, testAppId)
        );

        var repository = ServiceProvider.GetRequiredService<IRefreshTokenRepository>();

        // Act
        var deleted = await repository.DeleteEntityItemsAsync(
            UserSettingHelper.UserId,
            testAppId,
            exceptId: current.Id,
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(2, deleted);
        Assert.Equal([current.Id], TestDbHelper.GetRefreshTokenEntitiesWithNoTracking(UserSettingHelper.UserId).Select(t => t.Id));
    }

    [Fact]
    public async Task GetLastActiveAtByUserIdAsync_Gives_Each_Users_Latest_Activity_In_The_App()
    {
        // Arrange
        var otherUserId = CreateUsers();
        var testAppId = AppId(RegisteredApp.AppName);
        var latest = DateTime.UtcNow.AddMinutes(-1);

        var refreshed = BuildRefreshToken(UserSettingHelper.UserId, testAppId);
        refreshed.UpdatedAt = latest;

        var neverRefreshed = BuildRefreshToken(UserSettingHelper.UserId, testAppId);
        neverRefreshed.CreatedAt = latest.AddMinutes(-10);
        neverRefreshed.UpdatedAt = null;

        var otherApp = BuildRefreshToken(otherUserId, AppId(TestingWebAppFactory.OtherAppName));
        otherApp.UpdatedAt = DateTime.UtcNow;

        await AddRefreshTokensAsync(refreshed, neverRefreshed, otherApp);

        var repository = ServiceProvider.GetRequiredService<IRefreshTokenRepository>();

        // Act
        var lastActiveAt = await repository.GetLastActiveAtByUserIdAsync(
            testAppId,
            [UserSettingHelper.UserId, otherUserId],
            TestContext.Current.CancellationToken
        );

        // Assert
        var userLastActiveAt = Assert.Single(lastActiveAt).Value;

        Assert.NotNull(userLastActiveAt);
        Assert.Equal(latest, userLastActiveAt.Value, TimeSpan.FromMilliseconds(1));
    }

    private string CreateUsers()
    {
        TestDbHelper.CreateIdentityUser();

        var otherUser = new IdentityUser
        {
            Id = Guid.CreateVersion7().ToString(),
            UserName = "other.user",
            NormalizedUserName = "OTHER.USER",
        };

        var appDbContext = ServiceProvider.GetRequiredService<AppDbContext>();
        appDbContext.Users.Add(otherUser);
        appDbContext.SaveChanges();

        return otherUser.Id;
    }

    private int AppId(string appName) => TestDbHelper.GetRegisteredAppEntityByAppName(appName).Id;

    private async Task AddRefreshTokensAsync(params RefreshTokenEntity[] refreshTokens)
    {
        var appDbContext = ServiceProvider.GetRequiredService<AppDbContext>();

        appDbContext.RefreshToken.AddRange(refreshTokens);

        await appDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static RefreshTokenEntity BuildRefreshToken(string userId, int appId) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            AppId = appId,
            Token = Guid.NewGuid().ToString(),
            Expires = DateTime.UtcNow.AddDays(1),
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedBy = userId,
            UpdatedAt = DateTime.UtcNow,
        };
}
