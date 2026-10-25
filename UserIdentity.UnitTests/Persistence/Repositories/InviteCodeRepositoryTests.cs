using System.Linq;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using PolyzenKit.Common.Exceptions;
using PolyzenKit.Domain.RegisteredApps;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Domain.InviteCodes;
using UserIdentity.Persistence;
using UserIdentity.Persistence.Repositories.InviteCodes;

using Xunit;

namespace UserIdentity.UnitTests.Persistence.Repositories;

public class InviteCodeRepositoryTests
{
  [Fact]
  public async Task CreateEntityItem_Persists_InviteCode()
  {
    var context = GetAppDbContext();
    await SeedRegisteredApp(context);
    var repository = new InviteCodeRepository(context);
    var inviteCode = BuildInviteCode();

    repository.CreateEntityItem(inviteCode);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    var saved = await context.InviteCode.SingleOrDefaultAsync(x => x.Id == inviteCode.Id, cancellationToken: TestContext.Current.CancellationToken);
    Assert.NotNull(saved);
    Assert.Equal(inviteCode.InviteCode, saved!.InviteCode);
    Assert.Equal(inviteCode.UserEmail, saved.UserEmail);
  }

  [Fact]
  public async Task GetEntityByAlternateIdAsync_MustExist_Returns_Entity()
  {
    var context = GetAppDbContext();
    await SeedRegisteredApp(context);
    var repository = new InviteCodeRepository(context);
    var inviteCode = BuildInviteCode();

    context.InviteCode.Add(inviteCode);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    var result = await repository.GetEntityByAlternateIdAsync(new InviteCodeEntity
    {
      AppId = inviteCode.AppId,
      UserEmail = inviteCode.UserEmail
    }, QueryCondition.MUST_EXIST, TestContext.Current.CancellationToken);

    Assert.NotNull(result);
    Assert.Equal(inviteCode.Id, result!.Id);
    Assert.Equal(inviteCode.InviteCode, result.InviteCode);
  }

  [Fact]
  public async Task GetEntityByAlternateIdAsync_MustExist_When_Missing_Throws_NoRecordException()
  {
    var context = GetAppDbContext();
    var repository = new InviteCodeRepository(context);

    await Assert.ThrowsAsync<NoRecordException>(() => repository.GetEntityByAlternateIdAsync(new InviteCodeEntity
    {
      UserEmail = "notfound@example.com"
    }, QueryCondition.MUST_EXIST, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task GetEntityByAlternateIdAsync_MustNotExist_When_Exists_Throws_RecordExistsException()
  {
    var context = GetAppDbContext();
    await SeedRegisteredApp(context);
    var repository = new InviteCodeRepository(context);
    var inviteCode = BuildInviteCode();

    context.InviteCode.Add(inviteCode);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<RecordExistsException>(() => repository.GetEntityByAlternateIdAsync(new InviteCodeEntity
    {
      AppId = inviteCode.AppId,
      UserEmail = inviteCode.UserEmail
    }, QueryCondition.MUST_NOT_EXIST, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task GetEntityByAlternateIdAsync_MustNotExist_When_Missing_Returns_Null()
  {
    var context = GetAppDbContext();
    var repository = new InviteCodeRepository(context);

    var result = await repository.GetEntityByAlternateIdAsync(new InviteCodeEntity
    {
      UserEmail = "new@example.com"
    }, QueryCondition.MUST_NOT_EXIST, TestContext.Current.CancellationToken);

    Assert.Null(result);
  }

  [Fact]
  public async Task GetEntityByAlternateIdAsync_For_Another_App_Returns_Null()
  {
    var context = GetAppDbContext();
    await SeedRegisteredApp(context);
    var repository = new InviteCodeRepository(context);
    var inviteCode = BuildInviteCode();

    context.InviteCode.Add(inviteCode);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    var result = await repository.GetEntityByAlternateIdAsync(new InviteCodeEntity
    {
      AppId = inviteCode.AppId + 1,
      UserEmail = inviteCode.UserEmail
    }, QueryCondition.MAY_OR_MAY_NOT_EXIST, TestContext.Current.CancellationToken);

    Assert.Null(result);
  }

  [Fact]
  public async Task GetEntityDtoByIdAsync_Returns_Dto()
  {
    var context = GetAppDbContext();
    var app = await SeedRegisteredApp(context);
    var repository = new InviteCodeRepository(context);
    var inviteCode = BuildInviteCode();
    inviteCode.AppId = app.Id;

    context.InviteCode.Add(inviteCode);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    var result = await repository.GetEntityDtoByIdAsync(inviteCode.Id, TestContext.Current.CancellationToken);

    Assert.NotNull(result);
    Assert.Equal(inviteCode.Id, result.Id);
    Assert.Equal(inviteCode.InviteCode, result.InviteCode);
    Assert.Equal(inviteCode.UserEmail, result.UserEmail);
    Assert.Equal(app.AppName, result.AppName);
  }

  [Fact]
  public async Task GetEntityDtoByIdAsync_When_Not_Found_Throws_NoRecordException()
  {
    var context = GetAppDbContext();
    var repository = new InviteCodeRepository(context);

    await Assert.ThrowsAsync<NoRecordException>(() => repository.GetEntityDtoByIdAsync(999, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task GetEntityDtosByAppIdAsync_Returns_The_Apps_InviteCodes_Newest_First()
  {
    var context = GetAppDbContext();
    var app = await SeedRegisteredApp(context);
    context.RegisteredApp.Add(new RegisteredAppEntity { Id = 2, AppName = "OtherApp", AppSecretKey = "other-secret-key", BaseUrl = "https://other.com" });
    var repository = new InviteCodeRepository(context);
    var now = System.DateTime.UtcNow;

    for (var i = 0; i < 3; i++)
    {
      var inviteCode = BuildInviteCode();
      inviteCode.UserEmail = $"user{i}@example.com";
      inviteCode.CreatedAt = now.AddMinutes(i);
      inviteCode.Role = i == 2 ? "testapp:owner" : null;
      context.InviteCode.Add(inviteCode);
    }

    var otherAppInviteCode = BuildInviteCode();
    otherAppInviteCode.AppId = 2;
    context.InviteCode.Add(otherAppInviteCode);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    var page = await repository.GetEntityDtosByAppIdAsync(app.Id, 1, 2, TestContext.Current.CancellationToken);

    Assert.Equal(3, page.TotalCount);
    Assert.Equal(["user2@example.com", "user1@example.com"], page.Items.Select(i => i.UserEmail));
    Assert.Equal("testapp:owner", page.Items[0].Role);
  }

  private static AppDbContext GetAppDbContext()
  {
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(databaseName: System.Guid.NewGuid().ToString())
        .Options;

    return new AppDbContext(options);
  }

  private static async Task<RegisteredAppEntity> SeedRegisteredApp(AppDbContext context)
  {
    var app = new RegisteredAppEntity
    {
      Id = 1,
      AppName = "TestApp",
      AppSecretKey = "test-secret-key",
      BaseUrl = "https://test.com",
      RequireInviteCode = true
    };
    context.RegisteredApp.Add(app);
    await context.SaveChangesAsync();
    return app;
  }

  private static InviteCodeEntity BuildInviteCode()
  {
    return new InviteCodeEntity
    {
      InviteCode = "INVITE123",
      UserEmail = "test@example.com",
      AppId = 1,
      Applied = false,
      CreatedBy = "system",
      UpdatedBy = "system",
      IsDeleted = false
    };
  }
}
