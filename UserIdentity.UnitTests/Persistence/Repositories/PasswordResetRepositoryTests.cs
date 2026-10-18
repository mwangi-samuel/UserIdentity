using System;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using PolyzenKit.Common.Exceptions;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Domain.PasswordResets;
using UserIdentity.Persistence;
using UserIdentity.Persistence.Repositories.PasswordResets;

using Xunit;

namespace UserIdentity.UnitTests.Persistence.Repositories;

public class PasswordResetRepositoryTests
{
  [Fact]
  public async Task CreateEntityItem_Persists_PasswordReset()
  {
    var context = GetAppDbContext();
    var repository = new PasswordResetRepository(context);
    var passwordReset = BuildPasswordReset();

    repository.CreateEntityItem(passwordReset);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    var saved = await context.PasswordReset.SingleOrDefaultAsync(x => x.Id == passwordReset.Id, cancellationToken: TestContext.Current.CancellationToken);
    Assert.NotNull(saved);
    Assert.Equal(passwordReset.RequestId, saved!.RequestId);
  }

  [Fact]
  public async Task GetEntityByAlternateIdAsync_MustExist_Returns_Entity()
  {
    var context = GetAppDbContext();
    var repository = new PasswordResetRepository(context);
    var passwordReset = BuildPasswordReset();

    context.PasswordReset.Add(passwordReset);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    var result = await repository.GetEntityByAlternateIdAsync(new PasswordResetEntity
    {
      RequestId = passwordReset.RequestId
    }, QueryCondition.MUST_EXIST, TestContext.Current.CancellationToken);

    Assert.NotNull(result);
    Assert.Equal(passwordReset.Id, result!.Id);
  }

  [Fact]
  public async Task GetEntityByAlternateIdAsync_MustExist_When_Missing_Throws_NoRecordException()
  {
    var repository = new PasswordResetRepository(GetAppDbContext());

    await Assert.ThrowsAsync<NoRecordException>(() => repository.GetEntityByAlternateIdAsync(new PasswordResetEntity
    {
      RequestId = Guid.NewGuid().ToString()
    }, QueryCondition.MUST_EXIST, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task GetEntityByAlternateIdAsync_MayOrMayNotExist_When_Missing_Returns_Null()
  {
    var repository = new PasswordResetRepository(GetAppDbContext());

    var result = await repository.GetEntityByAlternateIdAsync(new PasswordResetEntity
    {
      RequestId = Guid.NewGuid().ToString()
    }, QueryCondition.MAY_OR_MAY_NOT_EXIST, TestContext.Current.CancellationToken);

    Assert.Null(result);
  }

  [Fact]
  public async Task GetLatestEntityItemAsync_Returns_The_Users_Most_Recent_Request()
  {
    var context = GetAppDbContext();
    var repository = new PasswordResetRepository(context);
    var olderRequest = BuildPasswordReset();
    olderRequest.CreatedAt = DateTime.UtcNow.AddMinutes(-10);
    var latestRequest = BuildPasswordReset();
    latestRequest.UserId = olderRequest.UserId;
    latestRequest.CreatedAt = DateTime.UtcNow;
    var otherUserRequest = BuildPasswordReset();
    otherUserRequest.CreatedAt = DateTime.UtcNow.AddMinutes(1);

    context.PasswordReset.AddRange(olderRequest, latestRequest, otherUserRequest);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    var result = await repository.GetLatestEntityItemAsync(olderRequest.UserId, TestContext.Current.CancellationToken);

    Assert.NotNull(result);
    Assert.Equal(latestRequest.Id, result!.Id);
  }

  [Fact]
  public async Task GetLatestEntityItemAsync_When_User_Has_No_Requests_Returns_Null()
  {
    var repository = new PasswordResetRepository(GetAppDbContext());

    var result = await repository.GetLatestEntityItemAsync(Guid.NewGuid().ToString(), TestContext.Current.CancellationToken);

    Assert.Null(result);
  }

  [Fact]
  public async Task RemoveExpiredEntityItemsAsync_Removes_Only_The_Users_Expired_Requests()
  {
    var context = GetAppDbContext();
    var repository = new PasswordResetRepository(context);
    var activeRequest = BuildPasswordReset();
    var expiredRequest = BuildPasswordReset();
    expiredRequest.UserId = activeRequest.UserId;
    expiredRequest.Expires = DateTime.UtcNow.AddMinutes(-10);
    var otherUserExpiredRequest = BuildPasswordReset();
    otherUserExpiredRequest.Expires = DateTime.UtcNow.AddMinutes(-10);

    context.PasswordReset.AddRange(activeRequest, expiredRequest, otherUserExpiredRequest);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    await repository.RemoveExpiredEntityItemsAsync(activeRequest.UserId, DateTime.UtcNow, TestContext.Current.CancellationToken);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    var remainingIds = context.PasswordReset.Select(x => x.Id).ToList();
    Assert.Contains(activeRequest.Id, remainingIds);
    Assert.DoesNotContain(expiredRequest.Id, remainingIds);
    Assert.Contains(otherUserExpiredRequest.Id, remainingIds);
  }

  private static AppDbContext GetAppDbContext()
  {
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
        .Options;

    return new AppDbContext(options);
  }

  private static PasswordResetEntity BuildPasswordReset()
  {
    return new PasswordResetEntity
    {
      Id = Guid.NewGuid(),
      RequestId = Guid.NewGuid().ToString(),
      UserId = Guid.NewGuid().ToString(),
      Expires = DateTime.UtcNow.AddMinutes(15),
      CreatedAt = DateTime.UtcNow
    };
  }
}
