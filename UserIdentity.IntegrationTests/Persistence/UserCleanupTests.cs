using System;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;

using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.IntegrationTests.TestUtils;

using Xunit;

namespace UserIdentity.IntegrationTests.Persistence;

public class UserCleanupTests(
    TestingWebAppFactory testingWebAppFactory,
    ITestOutputHelper outputHelper
    ) : BaseIntegrationTests(testingWebAppFactory, outputHelper)
{
  private void SeedAgedUser(bool emailConfirmed)
  {
    var identityUser = _testDbHelper.CreateIdentityUser();
    identityUser.EmailConfirmed = emailConfirmed;
    _testDbHelper.UpdateIdentityUser(identityUser);

    var userEntity = _testDbHelper.CreateUserEntity();
    userEntity.CreatedAt = DateTime.UtcNow.AddDays(-40);
    _testDbHelper.UpdateUserEntity(userEntity);
  }

  private Task DeleteUnconfirmedUsersAsync() =>
    _serviceProvider.GetRequiredService<IDeleteItemCommandHandler<DeleteUnconfirmedUsersCommand, DeleteRecordViewModel>>()
      .DeleteItemAsync(new DeleteUnconfirmedUsersCommand(), "system", TestContext.Current.CancellationToken);

  [Fact]
  public async Task DeleteUnconfirmedUsers_RemovesAnOldUnconfirmedUser()
  {
    // Arrange
    SeedAgedUser(emailConfirmed: false);

    // Act
    await DeleteUnconfirmedUsersAsync();

    // Assert
    Assert.Null(_testDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId));
    Assert.Null(_testDbHelper.GetUserEntity(UserSettingHelper.UserId));
  }

  [Fact]
  public async Task DeleteUnconfirmedUsers_KeepsAnOldConfirmedUser()
  {
    // Arrange
    SeedAgedUser(emailConfirmed: true);

    // Act
    await DeleteUnconfirmedUsersAsync();

    // Assert
    Assert.NotNull(_testDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId));
  }
}
