using Microsoft.EntityFrameworkCore;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Domain.RefreshTokens;
using UserIdentity.Domain.UserRegisteredApps;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using Xunit;

namespace UserIdentity.UnitTests.Persistence.Repositories.RefreshTokens;

public class RefreshTokenRepositoryTests
{
    [Fact]
    public async Task CreateEntityItem_Persists_RefreshToken()
    {
        // Arrange
        var context = AppDbContextTestFactory.GetAppDbContext();
        var repository = new RefreshTokenRepository(context);
        var refreshToken = BuildRefreshToken();

        // Act
        repository.CreateEntityItem(refreshToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var saved = await context.RefreshToken.SingleOrDefaultAsync(
            x => x.Id == refreshToken.Id,
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal(refreshToken.Token, saved!.Token);
    }

    [Fact]
    public async Task GetEntityByAlternateIdAsync_MustExist_Returns_Entity()
    {
        // Arrange
        var context = AppDbContextTestFactory.GetAppDbContext();
        var repository = new RefreshTokenRepository(context);
        var refreshToken = BuildRefreshToken();

        context.RefreshToken.Add(refreshToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await repository.GetEntityByAlternateIdAsync(
            new RefreshTokenEntity { UserId = refreshToken.UserId, Token = refreshToken.Token },
            QueryCondition.MustExist,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(refreshToken.Id, result!.Id);
    }

    [Fact]
    public async Task GetEntityByAlternateIdAsync_MustExist_When_Missing_Throws_NoRecordException()
    {
        // Arrange
        var repository = new RefreshTokenRepository(AppDbContextTestFactory.GetAppDbContext());

        // Act & Assert
        await Assert.ThrowsAsync<NoRecordException>(() =>
            repository.GetEntityByAlternateIdAsync(
                new RefreshTokenEntity { UserId = Guid.NewGuid().ToString(), Token = Guid.NewGuid().ToString() },
                QueryCondition.MustExist,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task GetEntityByAlternateIdAsync_MustNotExist_When_Exists_Throws_RecordExistsException()
    {
        // Arrange
        var context = AppDbContextTestFactory.GetAppDbContext();
        var repository = new RefreshTokenRepository(context);
        var refreshToken = BuildRefreshToken();

        context.RefreshToken.Add(refreshToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<RecordExistsException>(() =>
            repository.GetEntityByAlternateIdAsync(
                new RefreshTokenEntity { UserId = refreshToken.UserId, Token = refreshToken.Token },
                QueryCondition.MustNotExist,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task UpdateEntityItem_Updates_RefreshToken()
    {
        // Arrange
        var context = AppDbContextTestFactory.GetAppDbContext();
        var repository = new RefreshTokenRepository(context);
        var refreshToken = BuildRefreshToken();

        context.RefreshToken.Add(refreshToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        refreshToken.Expires = DateTime.UtcNow.AddMinutes(-10);

        // Act
        repository.UpdateEntityItem(refreshToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var saved = await context.RefreshToken.SingleAsync(
            x => x.Id == refreshToken.Id,
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal(refreshToken.Expires, saved.Expires);
    }

    [Fact]
    public async Task GetEntityByPreviousTokenAsync_Returns_Entity_With_Matching_Previous_Token()
    {
        // Arrange
        var context = AppDbContextTestFactory.GetAppDbContext();
        var repository = new RefreshTokenRepository(context);
        var refreshToken = BuildRefreshToken();
        refreshToken.PreviousToken = Guid.NewGuid().ToString();

        context.RefreshToken.Add(refreshToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await repository.GetEntityByPreviousTokenAsync(
            refreshToken.UserId,
            refreshToken.PreviousToken,
            TestContext.Current.CancellationToken
        );
        var otherUserResult = await repository.GetEntityByPreviousTokenAsync(
            Guid.NewGuid().ToString(),
            refreshToken.PreviousToken,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(refreshToken.Id, result!.Id);
        Assert.Null(otherUserResult);
    }

    [Theory]
    [InlineData(1, "TestApp", true)]
    [InlineData(1, "SecondApp", false)]
    [InlineData(2, "OtherApp", false)]
    [InlineData(3, "DeletedApp", false)]
    public async Task GetEntityForAppAsync_Returns_The_Tracked_Token_And_Whether_It_Is_A_Session_Of_The_Named_App_Its_User_Belongs_To(
        int tokenAppId,
        string appName,
        bool isForApp
    )
    {
        // Arrange
        var context = AppDbContextTestFactory.GetAppDbContext();
        var refreshToken = BuildRefreshToken();
        refreshToken.AppId = tokenAppId;
        context.RefreshToken.Add(refreshToken);
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
            },
            new RegisteredAppEntity
            {
                Id = 4,
                AppName = "SecondApp",
                AppSecretKey = "secret-key",
                BaseUrl = "https://second.test",
            }
        );
        context.UserRegisteredApp.AddRange(
            new UserRegisteredAppEntity { UserId = refreshToken.UserId, AppId = 1 },
            new UserRegisteredAppEntity { UserId = refreshToken.UserId, AppId = 3 },
            new UserRegisteredAppEntity { UserId = refreshToken.UserId, AppId = 4 },
            new UserRegisteredAppEntity { UserId = Guid.NewGuid().ToString(), AppId = 2 }
        );
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        // Act
        var result = await new RefreshTokenRepository(context).GetEntityForAppAsync(
            refreshToken.UserId,
            refreshToken.Token,
            appName,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(refreshToken.Id, result.Value.Entity.Id);
        Assert.Equal(isForApp, result.Value.IsForApp);
        Assert.Equal(EntityState.Unchanged, context.Entry(result.Value.Entity).State);
    }

    [Fact]
    public async Task GetEntityForAppAsync_For_Another_Users_Token_Returns_Null()
    {
        // Arrange
        var context = AppDbContextTestFactory.GetAppDbContext();
        var refreshToken = BuildRefreshToken();
        context.RefreshToken.Add(refreshToken);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await new RefreshTokenRepository(context).GetEntityForAppAsync(
            Guid.NewGuid().ToString(),
            refreshToken.Token,
            "TestApp",
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetEntityItemsAsync_Returns_The_Users_Unexpired_Tokens_Of_The_App_Last_Used_First_Untracked()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var context = AppDbContextTestFactory.GetAppDbContext();

        var usedLater = BuildRefreshToken();
        usedLater.CreatedAt = now.AddHours(-5);
        usedLater.UpdatedAt = now.AddHours(-1);

        var neverRefreshed = BuildRefreshToken();
        neverRefreshed.UserId = usedLater.UserId;
        neverRefreshed.CreatedAt = now.AddHours(-2);

        var expired = BuildRefreshToken();
        expired.UserId = usedLater.UserId;
        expired.Expires = now;

        var otherApp = BuildRefreshToken();
        otherApp.UserId = usedLater.UserId;
        otherApp.AppId = 2;

        context.RefreshToken.AddRange(usedLater, neverRefreshed, expired, otherApp, BuildRefreshToken());
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        // Act
        var result = await new RefreshTokenRepository(context).GetEntityItemsAsync(
            usedLater.UserId,
            1,
            now,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([usedLater.Id, neverRefreshed.Id], result.Select(token => token.Id));
        Assert.All(result, token => Assert.Equal(EntityState.Detached, context.Entry(token).State));
    }

    private static RefreshTokenEntity BuildRefreshToken()
    {
        return new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid().ToString(),
            AppId = 1,
            Token = Guid.NewGuid().ToString(),
            CreatedBy = Guid.NewGuid().ToString(),
            Expires = DateTime.UtcNow.AddMinutes(10),
        };
    }
}
