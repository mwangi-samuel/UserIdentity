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
