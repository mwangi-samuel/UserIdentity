using System.Collections.Generic;
using System.Threading.Tasks;

using PolyzenKit.Application.Core.Messages.Events;
using PolyzenKit.Common.Enums;

using UserIdentity.IntegrationTests.TestUtils;

using Xunit;

namespace UserIdentity.IntegrationTests.Infrastructure.Messaging;

public class CodeVerifiedMessageHandlerTests(
    TestingWebAppFactory testingWebAppFactory,
    ITestOutputHelper outputHelper
  ) : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
  [Fact]
  public async Task Welcome_User_Message_Confirms_The_User()
  {
    // Arrange
    var user = _testDbHelper.CreateIdentityUser();
    user.EmailConfirmed = false;
    _testDbHelper.UpdateIdentityUser(user);

    Assert.False(_testDbHelper.GetIdentityUserWithNoTracking(user.Id)?.EmailConfirmed);

    var messageEvent = MessageEventFactory.Create(user.Id, _registeredApp.AppName, new Dictionary<MessageAttribute, string>(), MessageAction.WELCOME_USER, CrudEvent.UPDATE);

    // Act
    await _natsTestHelper.PublishCodeVerifiedAsync(messageEvent);

    // Assert
    await _pollyResiliencePipeline.ExecuteAsync(token =>
    {
      Assert.True(_testDbHelper.GetIdentityUserWithNoTracking(user.Id)?.EmailConfirmed);
      return ValueTask.CompletedTask;
    }, TestContext.Current.CancellationToken);
  }

  [Fact]
  public async Task Password_Reset_Message_Marks_The_Reset_Request_Verified()
  {
    // Arrange
    const string resetRequestId = "reset-request-id-for-messaging";

    _testDbHelper.CreateIdentityUser();
    _testDbHelper.CreatePasswordResetEntity(resetRequestId, verified: false);

    var messageEvent = MessageEventFactory.Create(resetRequestId, _registeredApp.AppName, new Dictionary<MessageAttribute, string>(), MessageAction.PASSWORD_RESET, CrudEvent.UPDATE);

    // Act
    await _natsTestHelper.PublishCodeVerifiedAsync(messageEvent);

    // Assert
    await _pollyResiliencePipeline.ExecuteAsync(token =>
    {
      Assert.True(_testDbHelper.GetPasswordResetEntityWithNoTracking(UserSettingHelper.UserId)?.Verified);
      return ValueTask.CompletedTask;
    }, TestContext.Current.CancellationToken);
  }
}
