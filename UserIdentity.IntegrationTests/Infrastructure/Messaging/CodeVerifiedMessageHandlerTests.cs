using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.IntegrationTests.TestUtils;
using Xunit;

namespace UserIdentity.IntegrationTests.Infrastructure.Messaging;

public class CodeVerifiedMessageHandlerTests(TestingWebAppFactory testingWebAppFactory, ITestOutputHelper outputHelper)
    : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
    [Fact]
    public async Task Welcome_User_Message_Confirms_The_User()
    {
        // Arrange
        var user = TestDbHelper.CreateIdentityUser();
        user.EmailConfirmed = false;
        TestDbHelper.UpdateIdentityUser(user);

        Assert.False(TestDbHelper.GetIdentityUserWithNoTracking(user.Id)?.EmailConfirmed);

        // Act
        await NatsTestHelper.PublishCodeVerifiedAsync(user.Id, RegisteredApp.AppName, UserMessages.WelcomeUserAction);

        // Assert
        await Eventually.HoldsAsync(() => TestDbHelper.GetIdentityUserWithNoTracking(user.Id)?.EmailConfirmed == true);
    }

    [Fact]
    public async Task Welcome_User_Message_Delivered_Twice_Keeps_The_User_Confirmed()
    {
        // Arrange
        var user = TestDbHelper.CreateIdentityUser();
        user.EmailConfirmed = false;
        TestDbHelper.UpdateIdentityUser(user);

        // Act
        await NatsTestHelper.PublishCodeVerifiedAsync(user.Id, RegisteredApp.AppName, UserMessages.WelcomeUserAction);
        var secondDelivery = await NatsTestHelper.PublishCodeVerifiedAsync(user.Id, RegisteredApp.AppName, UserMessages.WelcomeUserAction);

        // Assert
        await Eventually.HoldsAsync(() => NatsTestHelper.IsCodeVerifiedAcknowledgedAsync(secondDelivery));

        Assert.True(TestDbHelper.GetIdentityUserWithNoTracking(user.Id)?.EmailConfirmed);
    }

    [Fact]
    public async Task Welcome_User_Message_With_Another_Status_Leaves_The_User_Unconfirmed()
    {
        // Arrange
        var user = TestDbHelper.CreateIdentityUser();
        user.EmailConfirmed = false;
        TestDbHelper.UpdateIdentityUser(user);

        // Act
        var delivery = await NatsTestHelper.PublishCodeVerifiedAsync(
            user.Id,
            RegisteredApp.AppName,
            UserMessages.WelcomeUserAction,
            status: "Expired"
        );

        // Assert
        await Eventually.HoldsAsync(() => NatsTestHelper.IsCodeVerifiedAcknowledgedAsync(delivery));

        Assert.False(TestDbHelper.GetIdentityUserWithNoTracking(user.Id)?.EmailConfirmed);
    }

    [Fact]
    public async Task Message_With_Another_Action_Leaves_The_User_Unconfirmed()
    {
        // Arrange
        var user = TestDbHelper.CreateIdentityUser();
        user.EmailConfirmed = false;
        TestDbHelper.UpdateIdentityUser(user);

        // Act
        var delivery = await NatsTestHelper.PublishCodeVerifiedAsync(user.Id, RegisteredApp.AppName, "OrderPlaced");

        // Assert
        await Eventually.HoldsAsync(() => NatsTestHelper.IsCodeVerifiedAcknowledgedAsync(delivery));

        Assert.False(TestDbHelper.GetIdentityUserWithNoTracking(user.Id)?.EmailConfirmed);
    }

    [Fact]
    public async Task Password_Reset_Message_Marks_The_Reset_Request_Verified()
    {
        // Arrange
        const string resetRequestId = "reset-request-id-for-messaging";

        TestDbHelper.CreateIdentityUser();
        TestDbHelper.CreatePasswordResetEntity(resetRequestId, verified: false);

        // Act
        await NatsTestHelper.PublishCodeVerifiedAsync(resetRequestId, RegisteredApp.AppName, UserMessages.PasswordResetAction);

        // Assert
        await Eventually.HoldsAsync(() => TestDbHelper.GetPasswordResetEntityWithNoTracking(UserSettingHelper.UserId)?.Verified == true);
    }
}
