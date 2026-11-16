using Microsoft.Extensions.DependencyInjection;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.IntegrationTests.TestUtils;
using UserIdentity.Persistence;
using UserIdentity.Persistence.Repositories.InviteCodes;
using Xunit;

namespace UserIdentity.IntegrationTests.Persistence.Repositories.InviteCodes;

public class InviteCodeRepositoryTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
    : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
    [Fact]
    public async Task GetAppliedEntityItemsForUserAsync_Returns_The_Applied_Invite_Of_The_Users_Own_App_Only()
    {
        // Arrange
        TestDbHelper.CreateIdentityUser();
        TestDbHelper.CreateUserEntity();

        var ownInvite = TestDbHelper.CreateInviteCodeEntity(
            UserSettingHelper.UserEmail.ToUpperInvariant(),
            RegisteredApp.AppName,
            applied: true
        );
        var otherAppInvite = BuildInvite(UserSettingHelper.UserEmail, TestingWebAppFactory.OtherAppName);
        var otherEmailInvite = BuildInvite("someone@example.com", RegisteredApp.AppName);

        var appDbContext = ServiceProvider.GetRequiredService<AppDbContext>();
        appDbContext.InviteCode.AddRange(otherAppInvite, otherEmailInvite);
        await appDbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = ServiceProvider.GetRequiredService<IInviteCodeRepository>();

        // Act
        var result = await repository.GetAppliedEntityItemsForUserAsync(UserSettingHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([ownInvite.Id], result.Select(invite => invite.Id));
    }

    [Fact]
    public async Task GetAppliedEntityItemsForUserAsync_Skips_An_Unused_Invite()
    {
        // Arrange
        TestDbHelper.CreateIdentityUser();
        TestDbHelper.CreateUserEntity();
        TestDbHelper.CreateInviteCodeEntity(UserSettingHelper.UserEmail, RegisteredApp.AppName);

        var repository = ServiceProvider.GetRequiredService<IInviteCodeRepository>();

        // Act
        var result = await repository.GetAppliedEntityItemsForUserAsync(UserSettingHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchEntityDtosByAppIdAsync_Filters_The_Whole_App_Before_Paging()
    {
        // Arrange
        var now = DateTime.UtcNow;

        for (var index = 0; index < 10; index++)
            TestDbHelper.CreateInviteCodeEntity(
                $"owner{index}@example.test",
                RegisteredApp.AppName,
                applied: index % 2 == 0,
                createdAt: now.AddMinutes(index)
            );

        TestDbHelper.CreateInviteCodeEntity("owner-other@example.test", TestingWebAppFactory.OtherAppName);

        var repository = ServiceProvider.GetRequiredService<IInviteCodeRepository>();
        var appId = TestDbHelper.GetRegisteredAppEntityByAppName(RegisteredApp.AppName).Id;

        // Act
        var result = await repository.SearchEntityDtosByAppIdAsync(appId, "owner", false, 2, 2, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(["owner5@example.test", "owner3@example.test"], result.Items.Select(invite => invite.UserEmail));
    }

    [Fact]
    public async Task SearchEntityDtosByAppIdAsync_Ignores_Case_And_Matches_Wildcards_Literally()
    {
        // Arrange
        TestDbHelper.CreateInviteCodeEntity("First_Owner@Example.test", RegisteredApp.AppName);
        TestDbHelper.CreateInviteCodeEntity("firstxowner@example.test", RegisteredApp.AppName);

        var repository = ServiceProvider.GetRequiredService<IInviteCodeRepository>();
        var appId = TestDbHelper.GetRegisteredAppEntityByAppName(RegisteredApp.AppName).Id;

        // Act
        var result = await repository.SearchEntityDtosByAppIdAsync(
            appId,
            "first_OWNER",
            null,
            1,
            10,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(["First_Owner@Example.test"], result.Items.Select(invite => invite.UserEmail));
    }

    private InviteCodeEntity BuildInvite(string userEmail, string appName) =>
        new()
        {
            InviteCode = "INVITE0123",
            UserEmail = userEmail,
            AppId = TestDbHelper.GetRegisteredAppEntityByAppName(appName).Id,
            Applied = true,
            CreatedBy = UserSettingHelper.UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedBy = UserSettingHelper.UserId,
            UpdatedAt = DateTime.UtcNow,
        };
}
