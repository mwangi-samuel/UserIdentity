using System;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;

using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Domain.RefreshTokens;
using UserIdentity.IntegrationTests.TestUtils;
using UserIdentity.Persistence;

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

  [Fact]
  public async Task DeleteUnconfirmedUsers_Frees_The_Invite_Of_The_Users_App_And_Leaves_Another_Apps_Invite()
  {
    // Arrange
    SeedAgedUser(emailConfirmed: false);
    var ownInvite = _testDbHelper.CreateInviteCodeEntity(UserSettingHelper.UserEmail, _registeredApp.AppName, applied: true);
    var otherAppInvite = new InviteCodeEntity
    {
      InviteCode = "OTHERAPPCODE",
      UserEmail = UserSettingHelper.UserEmail,
      AppId = _testDbHelper.GetRegisteredAppEntityByAppName(TestingWebAppFactory.OtherAppName).Id,
      Applied = true
    };

    var context = _serviceProvider.GetRequiredService<AppDbContext>();
    context.InviteCode.Add(otherAppInvite);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    // Act
    await DeleteUnconfirmedUsersAsync();

    // Assert
    Assert.Null(_testDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId));
    Assert.False(_testDbHelper.GetInviteCodeEntityWithNoTracking(ownInvite.Id)?.Applied);
    Assert.True(_testDbHelper.GetInviteCodeEntityWithNoTracking(otherAppInvite.Id)?.Applied);
  }

  [Fact]
  public async Task DeleteExpiredRefreshTokens_Deletes_Only_Expired_Tokens_Across_Batches()
  {
    // Arrange
    _testDbHelper.CreateIdentityUser();
    var now = DateTime.UtcNow;
    var context = _serviceProvider.GetRequiredService<AppDbContext>();

    context.RefreshToken.AddRange(Enumerable.Range(0, DeleteExpiredRefreshTokensCommandHandler.BatchSize + 20).Select(index => BuildRefreshToken(now.AddMinutes(-index - 1))));
    context.RefreshToken.AddRange(Enumerable.Range(0, 3).Select(index => BuildRefreshToken(now.AddDays(index + 1))));
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    // Act
    await _serviceProvider.GetRequiredService<IDeleteItemCommandHandler<DeleteExpiredRefreshTokensCommand, DeleteRecordViewModel>>()
      .DeleteItemAsync(new DeleteExpiredRefreshTokensCommand(), "system", TestContext.Current.CancellationToken);

    // Assert
    var remaining = _testDbHelper.GetRefreshTokenEntitiesWithNoTracking(UserSettingHelper.UserId);
    Assert.Equal(3, remaining.Count);
    Assert.All(remaining, token => Assert.True(token.Expires > now));
  }

  [Fact]
  public async Task DeleteExpiredPasswordResets_Deletes_Only_Requests_Expired_Before_The_Count_Window_Across_Batches()
  {
    // Arrange
    _testDbHelper.CreateIdentityUser();
    var now = DateTime.UtcNow;

    for (var index = 0; index < DeleteExpiredPasswordResetsCommandHandler.BatchSize + 20; index++)
      _testDbHelper.CreatePasswordResetEntity($"old-request-{index}", expires: now.AddDays(-2));

    var recentlyExpired = _testDbHelper.CreatePasswordResetEntity("recently-expired", expires: now.AddHours(-1));
    var active = _testDbHelper.CreatePasswordResetEntity("active", expires: now.AddMinutes(15));

    // Act
    await _serviceProvider.GetRequiredService<IDeleteItemCommandHandler<DeleteExpiredPasswordResetsCommand, DeleteRecordViewModel>>()
      .DeleteItemAsync(new DeleteExpiredPasswordResetsCommand(), "system", TestContext.Current.CancellationToken);

    // Assert
    var remaining = await _serviceProvider.GetRequiredService<AppDbContext>().PasswordReset.AsNoTracking()
      .Select(reset => reset.Id).ToListAsync(TestContext.Current.CancellationToken);
    Assert.Equal([recentlyExpired.Id, active.Id], remaining.Order());
  }

  private static RefreshTokenEntity BuildRefreshToken(DateTime expires) => new()
  {
    Id = Guid.CreateVersion7(),
    UserId = UserSettingHelper.UserId,
    Token = Guid.NewGuid().ToString("N"),
    Expires = expires,
    CreatedBy = UserSettingHelper.UserId,
    CreatedAt = DateTime.UtcNow
  };
}
