using PolyzenKit.Application.Core.Messages.Events;
using PolyzenKit.Common.Enums;
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

        var messageEvent = MessageEventFactory.Create(
            user.Id,
            RegisteredApp.AppName,
            new Dictionary<MessageAttributeKey, string>(),
            MessageAction.WelcomeUser,
            CrudEvent.Update
        );

        // Act
        await NatsTestHelper.PublishCodeVerifiedAsync(messageEvent);

        // Assert
        await Eventually.HoldsAsync(() => TestDbHelper.GetIdentityUserWithNoTracking(user.Id)?.EmailConfirmed == true);
    }

    [Fact]
    public async Task Password_Reset_Message_Marks_The_Reset_Request_Verified()
    {
        // Arrange
        const string resetRequestId = "reset-request-id-for-messaging";

        TestDbHelper.CreateIdentityUser();
        TestDbHelper.CreatePasswordResetEntity(resetRequestId, verified: false);

        var messageEvent = MessageEventFactory.Create(
            resetRequestId,
            RegisteredApp.AppName,
            new Dictionary<MessageAttributeKey, string>(),
            MessageAction.PasswordReset,
            CrudEvent.Update
        );

        // Act
        await NatsTestHelper.PublishCodeVerifiedAsync(messageEvent);

        // Assert
        await Eventually.HoldsAsync(() => TestDbHelper.GetPasswordResetEntityWithNoTracking(UserSettingHelper.UserId)?.Verified == true);
    }
}
