using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UserIdentity.Domain.WaitLists;
using UserIdentity.IntegrationTests.TestUtils;
using UserIdentity.Persistence;
using UserIdentity.Persistence.Repositories.WaitLists;
using Xunit;

namespace UserIdentity.IntegrationTests.Persistence.Repositories.WaitLists;

public class WaitListRepositoryTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
    : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
    [Fact]
    public async Task DeleteEntityItemsForUserAsync_Deletes_The_Entries_Holding_The_Users_Email_In_The_Users_Apps_Only()
    {
        // Arrange
        TestDbHelper.CreateIdentityUser();
        TestDbHelper.CreateUserEntity();

        var ownEntry = TestDbHelper.CreateWaitListEntity(UserSettingHelper.UserEmail.ToUpperInvariant(), RegisteredApp.AppName);
        var otherEmailEntry = TestDbHelper.CreateWaitListEntity("someone@example.com", RegisteredApp.AppName);
        var otherAppEntry = new WaitListEntity
        {
            UserEmail = UserSettingHelper.UserEmail,
            AppId = TestDbHelper.GetRegisteredAppEntityByAppName(TestingWebAppFactory.OtherAppName).Id,
            CreatedBy = UserSettingHelper.UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedBy = UserSettingHelper.UserId,
            UpdatedAt = DateTime.UtcNow,
        };

        var appDbContext = ServiceProvider.GetRequiredService<AppDbContext>();
        appDbContext.WaitList.Add(otherAppEntry);
        await appDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = ServiceProvider.GetRequiredService<IWaitListRepository>();

        // Act
        var deleted = await repository.DeleteEntityItemsForUserAsync(UserSettingHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, deleted);

        var remaining = await appDbContext
            .WaitList.AsNoTracking()
            .Select(entry => entry.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(ownEntry.Id, remaining);
        Assert.Contains(otherEmailEntry.Id, remaining);
        Assert.Contains(otherAppEntry.Id, remaining);
    }
}
