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
    var identityUser = TestDbHelper.CreateIdentityUser();
    identityUser.EmailConfirmed = emailConfirmed;
    TestDbHelper.UpdateIdentityUser(identityUser);

    var userEntity = TestDbHelper.CreateUserEntity();
    userEntity.CreatedAt = DateTime.UtcNow.AddDays(-40);
    TestDbHelper.UpdateUserEntity(userEntity);
  }

  private Task<DeleteRecordViewModel> DeleteUnconfirmedUsersAsync() =>
    ServiceProvider.GetRequiredService<IDeleteItemCommandHandler<DeleteUnconfirmedUsersCommand, DeleteRecordViewModel>>()
      .DeleteItemAsync(new DeleteUnconfirmedUsersCommand(), "system", TestContext.Current.CancellationToken);

  [Fact]
  public async Task DeleteUnconfirmedUsers_RemovesAnOldUnconfirmedUser()
  {
    // Arrange
    SeedAgedUser(emailConfirmed: false);

    // Act
    await DeleteUnconfirmedUsersAsync();

    // Assert
    Assert.Null(TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId));
    Assert.Null(TestDbHelper.GetUserEntity(UserSettingHelper.UserId));
  }

  [Fact]
  public async Task DeleteUnconfirmedUsers_KeepsAnOldConfirmedUser()
  {
    // Arrange
    SeedAgedUser(emailConfirmed: true);

    // Act
    await DeleteUnconfirmedUsersAsync();

    // Assert
    Assert.NotNull(TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId));
  }

  [Fact]
  public async Task DeleteUnconfirmedUsers_Frees_The_Invite_Of_The_Users_App_And_Leaves_Another_Apps_Invite()
  {
    // Arrange
    SeedAgedUser(emailConfirmed: false);
    var ownInvite = TestDbHelper.CreateInviteCodeEntity(
      UserSettingHelper.UserEmail,
      RegisteredApp.AppName,
      applied: true);
    var otherAppInvite = new InviteCodeEntity
    {
      InviteCode = "OTHERAPPCODE",
      UserEmail = UserSettingHelper.UserEmail,
      AppId = TestDbHelper.GetRegisteredAppEntityByAppName(TestingWebAppFactory.OtherAppName).Id,
      Applied = true
    };

    var context = ServiceProvider.GetRequiredService<AppDbContext>();
    context.InviteCode.Add(otherAppInvite);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    // Act
    await DeleteUnconfirmedUsersAsync();

    // Assert
    Assert.Null(TestDbHelper.GetIdentityUserWithNoTracking(UserSettingHelper.UserId));
    Assert.False(TestDbHelper.GetInviteCodeEntityWithNoTracking(ownInvite.Id)?.Applied);
    Assert.True(TestDbHelper.GetInviteCodeEntityWithNoTracking(otherAppInvite.Id)?.Applied);
  }

  [Fact]
  public async Task DeleteExpiredRefreshTokens_Deletes_Only_Expired_Tokens_Across_Batches()
  {
    // Arrange
    TestDbHelper.CreateIdentityUser();
    var now = DateTime.UtcNow;
    var context = ServiceProvider.GetRequiredService<AppDbContext>();

    context.RefreshToken.AddRange(
      Enumerable.Range(0, DeleteExpiredRefreshTokensCommandHandler.BatchSize + 20)
        .Select(index => BuildRefreshToken(now.AddMinutes(-index - 1))));
    context.RefreshToken.AddRange(Enumerable.Range(0, 3).Select(index => BuildRefreshToken(now.AddDays(index + 1))));
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    // Act
    await ServiceProvider.GetRequiredService<IDeleteItemCommandHandler<DeleteExpiredRefreshTokensCommand, DeleteRecordViewModel>>()
      .DeleteItemAsync(new DeleteExpiredRefreshTokensCommand(), "system", TestContext.Current.CancellationToken);

    // Assert
    var remaining = TestDbHelper.GetRefreshTokenEntitiesWithNoTracking(UserSettingHelper.UserId);
    Assert.Equal(3, remaining.Count);
    Assert.All(remaining, token => Assert.True(token.Expires > now));
  }

  [Fact]
  public async Task DeleteExpiredPasswordResets_Deletes_Only_Requests_Expired_Before_The_Count_Window_Across_Batches()
  {
    // Arrange
    TestDbHelper.CreateIdentityUser();
    var now = DateTime.UtcNow;

    for (var index = 0; index < DeleteExpiredPasswordResetsCommandHandler.BatchSize + 20; index++)
      TestDbHelper.CreatePasswordResetEntity($"old-request-{index}", expires: now.AddDays(-2));

    var recentlyExpired = TestDbHelper.CreatePasswordResetEntity("recently-expired", expires: now.AddHours(-1));
    var active = TestDbHelper.CreatePasswordResetEntity("active", expires: now.AddMinutes(15));

    // Act
    await ServiceProvider.GetRequiredService<IDeleteItemCommandHandler<DeleteExpiredPasswordResetsCommand, DeleteRecordViewModel>>()
      .DeleteItemAsync(new DeleteExpiredPasswordResetsCommand(), "system", TestContext.Current.CancellationToken);

    // Assert
    var remaining = await ServiceProvider.GetRequiredService<AppDbContext>().PasswordReset.AsNoTracking()
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
