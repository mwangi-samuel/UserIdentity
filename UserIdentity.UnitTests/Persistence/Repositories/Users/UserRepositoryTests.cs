using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using PolyzenKit.Common.Utilities;
using PolyzenKit.Domain.Entities.RegisteredApps;

using UserIdentity.Domain.UserRegisteredApps;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence;
using UserIdentity.Persistence.Repositories.Users;

using Xunit;

namespace UserIdentity.UnitTests.Persistence.Repositories.Users;

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

  [Fact]
  public async Task GetUserDtoAsync_Joins_The_Identity_User_And_Its_Details()
  {
    // Arrange
    var context = AppDbContextTestFactory.GetAppDbContext();
    var (user, details) = await SeedUserAsync(context, "TestApp");

    // Act
    var dto = await new UserRepository(context).GetUserDtoAsync(user.Id, "TestApp", TestContext.Current.CancellationToken);

    // Assert
    Assert.NotNull(dto);
    Assert.Equal(user.Id, dto.Id);
    Assert.Equal(user.UserName, dto.UserName);
    Assert.Equal(user.Email, dto.Email);
    Assert.Equal(user.PhoneNumber, dto.PhoneNumber);
    Assert.Equal(details.FirstName, dto.FirstName);
    Assert.Equal(details.LastName, dto.LastName);
    Assert.Equal(details.CreatedBy, dto.CreatedBy);
    Assert.Equal(details.CreatedAt, dto.CreatedAt);
  }

  [Fact]
  public async Task GetUserDtoAsync_Without_An_App_Reads_Any_Apps_User()
  {
    // Arrange
    var context = AppDbContextTestFactory.GetAppDbContext();
    var (user, _) = await SeedUserAsync(context, "TestApp");

    // Act
    var dto = await new UserRepository(context).GetUserDtoAsync(user.Id, null, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(user.Id, dto?.Id);
  }

  [Fact]
  public async Task GetUserDtoAsync_For_Another_Apps_User_Returns_Null()
  {
    // Arrange
    var context = AppDbContextTestFactory.GetAppDbContext();
    var (user, _) = await SeedUserAsync(context, "TestApp");
    context.RegisteredApp.Add(new RegisteredAppEntity { Id = 2, AppName = "OtherApp", AppSecretKey = "other-secret-key", BaseUrl = "https://other.test" });
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    // Act
    var dto = await new UserRepository(context).GetUserDtoAsync(user.Id, "OtherApp", TestContext.Current.CancellationToken);

    // Assert
    Assert.Null(dto);
  }

  [Fact]
  public async Task GetUserDtoAsync_For_A_Deleted_App_Returns_Null()
  {
    // Arrange
    var context = AppDbContextTestFactory.GetAppDbContext();
    var (user, _) = await SeedUserAsync(context, "TestApp", appDeleted: true);

    // Act
    var dto = await new UserRepository(context).GetUserDtoAsync(user.Id, "TestApp", TestContext.Current.CancellationToken);

    // Assert
    Assert.Null(dto);
  }

  [Fact]
  public async Task GetUserDtoAsync_For_An_Unknown_User_Returns_Null()
  {
    // Arrange
    var context = AppDbContextTestFactory.GetAppDbContext();

    // Act
    var dto = await new UserRepository(context).GetUserDtoAsync(Guid.NewGuid().ToString(), null, TestContext.Current.CancellationToken);

    // Assert
    Assert.Null(dto);
  }

  private static async Task<(IdentityUser User, UserEntity Details)> SeedUserAsync(AppDbContext context, string appName, bool appDeleted = false)
  {
    var user = new IdentityUser { Id = Guid.NewGuid().ToString(), UserName = "test.user", Email = "test.user@example.com", PhoneNumber = "0712345678" };
    var details = BuildUser();
    details.Id = user.Id;
    details.CreatedBy = user.Id;
    details.CreatedAt = DateTime.UtcNow;

    var app = new RegisteredAppEntity { Id = 1, AppName = appName, AppSecretKey = "secret-key", BaseUrl = "https://app.test", IsDeleted = appDeleted };

    context.Users.Add(user);
    context.AppUser.Add(details);
    context.RegisteredApp.Add(app);
    context.UserRegisteredApp.Add(new UserRegisteredAppEntity { Id = $"{user.Id}-{app.Id}", UserId = user.Id, AppId = app.Id });
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    return (user, details);
  }

  private static UserEntity BuildUser()
  {
    return new UserEntity
    {
      Id = Guid.NewGuid().ToString(),
      FirstName = RandomStrings.Generate(12),
      LastName = RandomStrings.Generate(8)
    };
  }
}
