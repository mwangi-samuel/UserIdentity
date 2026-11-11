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
        TestDbHelper.SeedDatabase();

        var otherUser = new IdentityUser
        {
            Id = Guid.CreateVersion7().ToString(),
            UserName = "other.user",
            NormalizedUserName = "OTHER.USER",
        };

        var appDbContext = ServiceProvider.GetRequiredService<AppDbContext>();

        appDbContext.Users.Add(otherUser);
        appDbContext.RefreshToken.AddRange(BuildRefreshToken(UserSettingHelper.UserId), BuildRefreshToken(otherUser.Id));

        await appDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = ServiceProvider.GetRequiredService<IRefreshTokenRepository>();

        // Act
        await repository.DeleteEntityItemsAsync(UserSettingHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(TestDbHelper.GetRefreshTokenEntitiesWithNoTracking(UserSettingHelper.UserId));
        Assert.Single(TestDbHelper.GetRefreshTokenEntitiesWithNoTracking(otherUser.Id));
    }

    private static RefreshTokenEntity BuildRefreshToken(string userId) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Token = Guid.NewGuid().ToString(),
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedBy = userId,
            UpdatedAt = DateTime.UtcNow,
        };
}
