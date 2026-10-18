using System;
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
