using System;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using PolyzenKit.Common.Utilities;

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

  [Fact]
  public async Task GetUnconfirmedEntityItemsCreatedBeforeAsync_ReturnsOnlyOldUnconfirmedUsers()
  {
    // Arrange
    var now = DateTime.UtcNow;
    var context = AppDbContextTestFactory.GetAppDbContext();
    var oldUnconfirmed = BuildUser();
    var oldConfirmed = BuildUser();
    var freshUnconfirmed = BuildUser();
    oldUnconfirmed.CreatedAt = now.AddDays(-40);
    oldConfirmed.CreatedAt = now.AddDays(-40);
    freshUnconfirmed.CreatedAt = now;

    context.AppUser.AddRange(oldUnconfirmed, oldConfirmed, freshUnconfirmed);
    context.Users.AddRange(
      new IdentityUser { Id = oldUnconfirmed.Id, EmailConfirmed = false },
      new IdentityUser { Id = oldConfirmed.Id, EmailConfirmed = true },
      new IdentityUser { Id = freshUnconfirmed.Id, EmailConfirmed = false });
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    var repository = new UserRepository(context);

    // Act
    var result = await repository.GetUnconfirmedEntityItemsCreatedBeforeAsync(now.AddDays(-30), 10, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal([oldUnconfirmed.Id], result.Select(e => e.Id));
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
