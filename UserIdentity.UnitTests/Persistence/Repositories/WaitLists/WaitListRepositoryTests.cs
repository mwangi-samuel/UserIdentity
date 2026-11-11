using Microsoft.EntityFrameworkCore;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Domain.WaitLists;
using UserIdentity.Persistence;
using UserIdentity.Persistence.Repositories.WaitLists;
using Xunit;

namespace UserIdentity.UnitTests.Persistence.Repositories.WaitLists;

public class WaitListRepositoryTests
{
    [Fact]
    public async Task CreateEntityItem_Persists_WaitList()
    {
        // Arrange
        var context = GetAppDbContext();
        await SeedRegisteredApp(context);
        var repository = new WaitListRepository(context);
        var waitList = BuildWaitList();

        // Act
        repository.CreateEntityItem(waitList);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var saved = await context.WaitList.SingleOrDefaultAsync(
            x => x.Id == waitList.Id,
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal(waitList.UserEmail, saved!.UserEmail);
    }

    [Fact]
    public async Task GetEntityByAlternateIdAsync_MustExist_Returns_Entity()
    {
        // Arrange
        var context = GetAppDbContext();
        await SeedRegisteredApp(context);
        var repository = new WaitListRepository(context);
        var waitList = BuildWaitList();

        context.WaitList.Add(waitList);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await repository.GetEntityByAlternateIdAsync(
            new WaitListEntity { AppId = waitList.AppId, UserEmail = waitList.UserEmail },
            QueryCondition.MustExist,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(waitList.Id, result!.Id);
        Assert.Equal(waitList.UserEmail, result.UserEmail);
    }

    [Fact]
    public async Task GetEntityByAlternateIdAsync_MustExist_When_Missing_Throws_NoRecordException()
    {
        // Arrange
        var repository = new WaitListRepository(GetAppDbContext());

        // Act & Assert
        await Assert.ThrowsAsync<NoRecordException>(() =>
            repository.GetEntityByAlternateIdAsync(
                new WaitListEntity { UserEmail = "notfound@example.com" },
                QueryCondition.MustExist,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task GetEntityByAlternateIdAsync_MustNotExist_When_Exists_Throws_RecordExistsException()
    {
        // Arrange
        var context = GetAppDbContext();
        await SeedRegisteredApp(context);
        var repository = new WaitListRepository(context);
        var waitList = BuildWaitList();

        context.WaitList.Add(waitList);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<RecordExistsException>(() =>
            repository.GetEntityByAlternateIdAsync(
                new WaitListEntity { AppId = waitList.AppId, UserEmail = waitList.UserEmail },
                QueryCondition.MustNotExist,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task GetEntityByAlternateIdAsync_MustNotExist_When_Missing_Returns_Null()
    {
        // Arrange
        var repository = new WaitListRepository(GetAppDbContext());

        // Act
        var result = await repository.GetEntityByAlternateIdAsync(
            new WaitListEntity { UserEmail = "new@example.com" },
            QueryCondition.MustNotExist,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetEntityDtoByIdAsync_Returns_Dto()
    {
        // Arrange
        var context = GetAppDbContext();
        var app = await SeedRegisteredApp(context);
        var repository = new WaitListRepository(context);
        var waitList = BuildWaitList();
        waitList.AppId = app.Id;

        context.WaitList.Add(waitList);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await repository.GetEntityDtoByIdAsync(waitList.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(waitList.Id, result.Id);
        Assert.Equal(waitList.UserEmail, result.UserEmail);
        Assert.Equal(app.AppName, result.AppName);
    }

    [Fact]
    public async Task GetEntityDtoByIdAsync_When_Not_Found_Throws_NoRecordException()
    {
        // Arrange
        var repository = new WaitListRepository(GetAppDbContext());

        // Act & Assert
        await Assert.ThrowsAsync<NoRecordException>(() => repository.GetEntityDtoByIdAsync(999, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetEntityDtoByEmailAsync_Returns_The_Apps_Entry_For_That_Email_Without_Tracking_It()
    {
        // Arrange
        var context = GetAppDbContext();
        await SeedRegisteredApp(context);
        var waitList = BuildWaitList();
        context.WaitList.Add(waitList);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        var repository = new WaitListRepository(context);

        // Act
        var dto = await repository.GetEntityDtoByEmailAsync(1, waitList.UserEmail, TestContext.Current.CancellationToken);
        var otherAppDto = await repository.GetEntityDtoByEmailAsync(2, waitList.UserEmail, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(waitList.Id, dto.Id);
        Assert.Equal("TestApp", dto.AppName);
        Assert.Null(otherAppDto);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    private static AppDbContext GetAppDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

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
            RequireInviteCode = false,
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
            IsDeleted = false,
        };
    }
}
