using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using PolyzenKit.Common.Exceptions;
using PolyzenKit.Domain.RegisteredApps;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Domain.WaitLists;
using UserIdentity.Persistence;
using UserIdentity.Persistence.Repositories.WaitLists;

using Xunit;

namespace UserIdentity.UnitTests.Persistence.Repositories;

public class WaitListRepositoryTests
{
  [Fact]
  public async Task CreateEntityItem_Persists_WaitList()
  {
    var context = GetAppDbContext();
    await SeedRegisteredApp(context);
    var repository = new WaitListRepository(context);
    var waitList = BuildWaitList();

    repository.CreateEntityItem(waitList);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    var saved = await context.WaitList.SingleOrDefaultAsync(x => x.Id == waitList.Id, cancellationToken: TestContext.Current.CancellationToken);
    Assert.NotNull(saved);
    Assert.Equal(waitList.UserEmail, saved!.UserEmail);
  }

  [Fact]
  public async Task GetEntityByAlternateIdAsync_MustExist_Returns_Entity()
  {
    var context = GetAppDbContext();
    await SeedRegisteredApp(context);
    var repository = new WaitListRepository(context);
    var waitList = BuildWaitList();

    context.WaitList.Add(waitList);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    var result = await repository.GetEntityByAlternateIdAsync(new WaitListEntity
    {
      UserEmail = waitList.UserEmail
    }, QueryCondition.MUST_EXIST, TestContext.Current.CancellationToken);

    Assert.NotNull(result);
    Assert.Equal(waitList.Id, result!.Id);
    Assert.Equal(waitList.UserEmail, result.UserEmail);
  }

  [Fact]
  public async Task GetEntityByAlternateIdAsync_MustExist_When_Missing_Throws_NoRecordException()
  {
    var context = GetAppDbContext();
    var repository = new WaitListRepository(context);

    await Assert.ThrowsAsync<NoRecordException>(() => repository.GetEntityByAlternateIdAsync(new WaitListEntity
    {
      UserEmail = "notfound@example.com"
    }, QueryCondition.MUST_EXIST, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task GetEntityByAlternateIdAsync_MustNotExist_When_Exists_Throws_RecordExistsException()
  {
    var context = GetAppDbContext();
    await SeedRegisteredApp(context);
    var repository = new WaitListRepository(context);
    var waitList = BuildWaitList();

    context.WaitList.Add(waitList);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<RecordExistsException>(() => repository.GetEntityByAlternateIdAsync(new WaitListEntity
    {
      UserEmail = waitList.UserEmail
    }, QueryCondition.MUST_NOT_EXIST, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task GetEntityByAlternateIdAsync_MustNotExist_When_Missing_Returns_Null()
  {
    var context = GetAppDbContext();
    var repository = new WaitListRepository(context);

    var result = await repository.GetEntityByAlternateIdAsync(new WaitListEntity
    {
      UserEmail = "new@example.com"
    }, QueryCondition.MUST_NOT_EXIST, TestContext.Current.CancellationToken);

    Assert.Null(result);
  }

  [Fact]
  public async Task GetEntityDTOByIdAsync_Returns_DTO()
  {
    var context = GetAppDbContext();
    var app = await SeedRegisteredApp(context);
    var repository = new WaitListRepository(context);
    var waitList = BuildWaitList();
    waitList.AppId = app.Id;

    context.WaitList.Add(waitList);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    var result = await repository.GetEntityDTOByIdAsync(waitList.Id, TestContext.Current.CancellationToken);

    Assert.NotNull(result);
    Assert.Equal(waitList.Id, result.Id);
    Assert.Equal(waitList.UserEmail, result.UserEmail);
    Assert.Equal(app.AppName, result.AppName);
  }

  [Fact]
  public async Task GetEntityDTOByIdAsync_When_Not_Found_Throws_NoRecordException()
  {
    var context = GetAppDbContext();
    var repository = new WaitListRepository(context);

    await Assert.ThrowsAsync<NoRecordException>(() => repository.GetEntityDTOByIdAsync(999, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task GetEntityDTOByIdAsync_When_Deleted_Throws_NoRecordException()
  {
    var context = GetAppDbContext();
    var app = await SeedRegisteredApp(context);
    var repository = new WaitListRepository(context);
    var waitList = BuildWaitList();
    waitList.AppId = app.Id;
    waitList.IsDeleted = true;

    context.WaitList.Add(waitList);
    await context.SaveChangesAsync(TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<NoRecordException>(() => repository.GetEntityDTOByIdAsync(waitList.Id, TestContext.Current.CancellationToken));
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
      RequireInviteCode = false
    };
    context.RegisteredApp.Add(app);
    await context.SaveChangesAsync();
    return app;
  }

  private static WaitListEntity BuildWaitList()
  {
    return new WaitListEntity
    {
      UserEmail = "waitlist@example.com",
      AppId = 1,
      CreatedBy = "system",
      UpdatedBy = "system",
      IsDeleted = false
    };
  }
}
