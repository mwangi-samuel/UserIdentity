using System;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using PolyzenKit.Common.Exceptions;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Domain.RefreshTokens;
using UserIdentity.Persistence.Repositories.RefreshTokens;

using Xunit;

namespace UserIdentity.UnitTests.Persistence.Repositories;

public class RefreshTokenRepositoryTests
{
  [Fact]
  public async Task CreateEntityItem_Persists_RefreshToken()
  {
    var context = AppDbContextTestFactory.GetAppDbContext();
    var repository = new RefreshTokenRepository(context);
    var refreshToken = BuildRefreshToken();

    repository.CreateEntityItem(refreshToken);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    var saved = await context.RefreshToken.SingleOrDefaultAsync(x => x.Id == refreshToken.Id, cancellationToken: TestContext.Current.CancellationToken);
    Assert.NotNull(saved);
    Assert.Equal(refreshToken.Token, saved!.Token);
  }

  [Fact]
  public async Task GetEntityByAlternateIdAsync_MustExist_Returns_Entity()
  {
    var context = AppDbContextTestFactory.GetAppDbContext();
    var repository = new RefreshTokenRepository(context);
    var refreshToken = BuildRefreshToken();

    context.RefreshToken.Add(refreshToken);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    var result = await repository.GetEntityByAlternateIdAsync(new RefreshTokenEntity
    {
      UserId = refreshToken.UserId,
      Token = refreshToken.Token
    }, QueryCondition.MUST_EXIST, TestContext.Current.CancellationToken);

    Assert.NotNull(result);
    Assert.Equal(refreshToken.Id, result!.Id);
  }

  [Fact]
  public async Task GetEntityByAlternateIdAsync_MustExist_When_Missing_Throws_NoRecordException()
  {
    var repository = new RefreshTokenRepository(AppDbContextTestFactory.GetAppDbContext());

    await Assert.ThrowsAsync<NoRecordException>(() => repository.GetEntityByAlternateIdAsync(new RefreshTokenEntity
    {
      UserId = Guid.NewGuid().ToString(),
      Token = Guid.NewGuid().ToString()
    }, QueryCondition.MUST_EXIST, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task GetEntityByAlternateIdAsync_MustNotExist_When_Exists_Throws_RecordExistsException()
  {
    var context = AppDbContextTestFactory.GetAppDbContext();
    var repository = new RefreshTokenRepository(context);
    var refreshToken = BuildRefreshToken();

    context.RefreshToken.Add(refreshToken);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<RecordExistsException>(() => repository.GetEntityByAlternateIdAsync(new RefreshTokenEntity
    {
      UserId = refreshToken.UserId,
      Token = refreshToken.Token
    }, QueryCondition.MUST_NOT_EXIST, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task UpdateEntityItem_Updates_RefreshToken()
  {
    var context = AppDbContextTestFactory.GetAppDbContext();
    var repository = new RefreshTokenRepository(context);
    var refreshToken = BuildRefreshToken();

    context.RefreshToken.Add(refreshToken);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    refreshToken.Expires = DateTime.UtcNow.AddMinutes(-10);
    repository.UpdateEntityItem(refreshToken);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    var saved = await context.RefreshToken.SingleAsync(x => x.Id == refreshToken.Id, cancellationToken: TestContext.Current.CancellationToken);
    Assert.Equal(refreshToken.Expires, saved.Expires);
  }

  [Fact]
  public async Task GetEntityByPreviousTokenAsync_Returns_Entity_With_Matching_Previous_Token()
  {
    var context = AppDbContextTestFactory.GetAppDbContext();
    var repository = new RefreshTokenRepository(context);
    var refreshToken = BuildRefreshToken();
    refreshToken.PreviousToken = Guid.NewGuid().ToString();

    context.RefreshToken.Add(refreshToken);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    var result = await repository.GetEntityByPreviousTokenAsync(refreshToken.UserId!, refreshToken.PreviousToken, TestContext.Current.CancellationToken);
    var otherUserResult = await repository.GetEntityByPreviousTokenAsync(Guid.NewGuid().ToString(), refreshToken.PreviousToken, TestContext.Current.CancellationToken);

    Assert.NotNull(result);
    Assert.Equal(refreshToken.Id, result!.Id);
    Assert.Null(otherUserResult);
  }

  [Fact]
  public async Task RemoveExpiredEntityItemsAsync_Removes_Only_The_Users_Expired_RefreshTokens()
  {
    var context = AppDbContextTestFactory.GetAppDbContext();
    var repository = new RefreshTokenRepository(context);
    var activeToken = BuildRefreshToken();
    var expiredToken = BuildRefreshToken();
    expiredToken.UserId = activeToken.UserId;
    expiredToken.Expires = DateTime.UtcNow.AddMinutes(-10);
    var otherUserExpiredToken = BuildRefreshToken();
    otherUserExpiredToken.Expires = DateTime.UtcNow.AddMinutes(-10);

    context.RefreshToken.AddRange(activeToken, expiredToken, otherUserExpiredToken);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    await repository.RemoveExpiredEntityItemsAsync(activeToken.UserId!, DateTime.UtcNow, TestContext.Current.CancellationToken);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    var remainingIds = context.RefreshToken.Select(x => x.Id).ToList();
    Assert.Contains(activeToken.Id, remainingIds);
    Assert.DoesNotContain(expiredToken.Id, remainingIds);
    Assert.Contains(otherUserExpiredToken.Id, remainingIds);
  }

  [Fact]
  public async Task RemoveEntityItemsAsync_Removes_All_The_Users_RefreshTokens()
  {
    var context = AppDbContextTestFactory.GetAppDbContext();
    var repository = new RefreshTokenRepository(context);
    var firstToken = BuildRefreshToken();
    var secondToken = BuildRefreshToken();
    secondToken.UserId = firstToken.UserId;
    var otherUserToken = BuildRefreshToken();

    context.RefreshToken.AddRange(firstToken, secondToken, otherUserToken);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    await repository.RemoveEntityItemsAsync(firstToken.UserId!, TestContext.Current.CancellationToken);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    var remainingIds = context.RefreshToken.Select(x => x.Id).ToList();
    Assert.DoesNotContain(firstToken.Id, remainingIds);
    Assert.DoesNotContain(secondToken.Id, remainingIds);
    Assert.Contains(otherUserToken.Id, remainingIds);
  }

  [Fact]
  public async Task GetEntityItemsExpiredBeforeAsync_ReturnsTheOldestExpiredTokens()
  {
    // Arrange
    var now = DateTime.UtcNow;
    var context = AppDbContextTestFactory.GetAppDbContext();
    var first = BuildRefreshToken();
    var second = BuildRefreshToken();
    var fresh = BuildRefreshToken();
    first.Expires = now.AddMinutes(-20);
    second.Expires = now.AddMinutes(-10);
    context.RefreshToken.AddRange(second, fresh, first);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    var repository = new RefreshTokenRepository(context);

    // Act
    var all = await repository.GetEntityItemsExpiredBeforeAsync(now, 10, TestContext.Current.CancellationToken);
    var limited = await repository.GetEntityItemsExpiredBeforeAsync(now, 1, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal([first.Id, second.Id], all.Select(e => e.Id));
    Assert.Equal([first.Id], limited.Select(e => e.Id));
  }

  private static RefreshTokenEntity BuildRefreshToken()
  {
    return new RefreshTokenEntity
    {
      Id = Guid.NewGuid(),
      UserId = Guid.NewGuid().ToString(),
      RemoteIpAddress = Guid.NewGuid().ToString(),
      Token = Guid.NewGuid().ToString(),
      CreatedBy = Guid.NewGuid().ToString(),
      Expires = DateTime.UtcNow.AddMinutes(10)
    };
  }
}
