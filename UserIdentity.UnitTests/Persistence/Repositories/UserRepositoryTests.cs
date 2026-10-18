using System;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.Users;

using Xunit;

namespace UserIdentity.UnitTests.Persistence.Repositories;

public class UserRepositoryTests
{
  [Fact]
  public async Task CreateEntityItem_Persists_User()
  {
    var context = AppDbContextTestFactory.GetAppDbContext();
    var repository = new UserRepository(context);
    var newUser = BuildUser();

    repository.CreateEntityItem(newUser);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    var saved = await context.AppUser.SingleOrDefaultAsync(x => x.Id == newUser.Id, cancellationToken: TestContext.Current.CancellationToken);
    Assert.NotNull(saved);
    Assert.Equal(newUser.FirstName, saved!.FirstName);
  }

  [Fact]
  public async Task GetEntityItemAsync_Returns_User_When_Found()
  {
    var context = AppDbContextTestFactory.GetAppDbContext();
    var repository = new UserRepository(context);
    var newUser = BuildUser();

    context.AppUser.Add(newUser);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    var result = await repository.GetEntityItemAsync(newUser.Id, TestContext.Current.CancellationToken);
    Assert.NotNull(result);
    Assert.Equal(newUser.Id, result!.Id);
  }

  [Fact]
  public async Task GetEntityByAlternateIdAsync_MustExist_Returns_User()
  {
    var context = AppDbContextTestFactory.GetAppDbContext();
    var repository = new UserRepository(context);
    var newUser = BuildUser();
    newUser.ForgotPasswordToken = StringUtil.GenerateRandomString(64);

    context.AppUser.Add(newUser);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    var result = await repository.GetEntityByAlternateIdAsync(new UserEntity
    {
      Id = newUser.Id,
      ForgotPasswordToken = newUser.ForgotPasswordToken
    }, QueryCondition.MUST_EXIST, TestContext.Current.CancellationToken);

    Assert.NotNull(result);
    Assert.Equal(newUser.Id, result!.Id);
  }

  [Fact]
  public async Task GetEntityByAlternateIdAsync_MustExist_When_Missing_Throws_NoRecordException()
  {
    var repository = new UserRepository(AppDbContextTestFactory.GetAppDbContext());

    await Assert.ThrowsAsync<NoRecordException>(() => repository.GetEntityByAlternateIdAsync(new UserEntity
    {
      Id = Guid.NewGuid().ToString(),
      ForgotPasswordToken = StringUtil.GenerateRandomString(32)
    }, QueryCondition.MUST_EXIST, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task GetEntityByAlternateIdAsync_MustNotExist_When_Exists_Throws_RecordExistsException()
  {
    var context = AppDbContextTestFactory.GetAppDbContext();
    var repository = new UserRepository(context);
    var newUser = BuildUser();
    newUser.ForgotPasswordToken = StringUtil.GenerateRandomString(64);

    context.AppUser.Add(newUser);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<RecordExistsException>(() => repository.GetEntityByAlternateIdAsync(new UserEntity
    {
      Id = newUser.Id,
      ForgotPasswordToken = newUser.ForgotPasswordToken
    }, QueryCondition.MUST_NOT_EXIST, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task UpdateEntityItem_Updates_User()
  {
    var context = AppDbContextTestFactory.GetAppDbContext();
    var repository = new UserRepository(context);
    var newUser = BuildUser();

    context.AppUser.Add(newUser);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    newUser.FirstName = "Updated";
    repository.UpdateEntityItem(newUser);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    var saved = await context.AppUser.SingleAsync(x => x.Id == newUser.Id, cancellationToken: TestContext.Current.CancellationToken);
    Assert.Equal("Updated", saved.FirstName);
  }

  private static UserEntity BuildUser()
  {
    return new UserEntity
    {
      Id = Guid.NewGuid().ToString(),
      FirstName = StringUtil.GenerateRandomString(12),
      LastName = StringUtil.GenerateRandomString(8)
    };
  }
}
