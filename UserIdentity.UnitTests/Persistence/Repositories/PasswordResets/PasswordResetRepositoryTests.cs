using Microsoft.EntityFrameworkCore;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Domain.PasswordResets;
using UserIdentity.Persistence.Repositories.PasswordResets;
using Xunit;

namespace UserIdentity.UnitTests.Persistence.Repositories.PasswordResets;

public class PasswordResetRepositoryTests
{
    [Fact]
    public async Task CreateEntityItem_Persists_PasswordReset()
    {
        var context = AppDbContextTestFactory.GetAppDbContext();
        var repository = new PasswordResetRepository(context);
        var passwordReset = BuildPasswordReset();

        repository.CreateEntityItem(passwordReset);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var saved = await context.PasswordReset.SingleOrDefaultAsync(
            x => x.Id == passwordReset.Id,
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.NotNull(saved);
        Assert.Equal(passwordReset.RequestId, saved!.RequestId);
    }

    [Fact]
    public async Task GetEntityByAlternateIdAsync_MustExist_Returns_Entity()
    {
        var context = AppDbContextTestFactory.GetAppDbContext();
        var repository = new PasswordResetRepository(context);
        var passwordReset = BuildPasswordReset();

        context.PasswordReset.Add(passwordReset);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await repository.GetEntityByAlternateIdAsync(
            new PasswordResetEntity { RequestId = passwordReset.RequestId },
            QueryCondition.MustExist,
            TestContext.Current.CancellationToken
        );

        Assert.NotNull(result);
        Assert.Equal(passwordReset.Id, result!.Id);
    }

    [Fact]
    public async Task GetEntityByAlternateIdAsync_MustExist_When_Missing_Throws_NoRecordException()
    {
        var repository = new PasswordResetRepository(AppDbContextTestFactory.GetAppDbContext());

        await Assert.ThrowsAsync<NoRecordException>(() =>
            repository.GetEntityByAlternateIdAsync(
                new PasswordResetEntity { RequestId = Guid.NewGuid().ToString() },
                QueryCondition.MustExist,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task GetEntityByAlternateIdAsync_MayOrMayNotExist_When_Missing_Returns_Null()
    {
        var repository = new PasswordResetRepository(AppDbContextTestFactory.GetAppDbContext());

        var result = await repository.GetEntityByAlternateIdAsync(
            new PasswordResetEntity { RequestId = Guid.NewGuid().ToString() },
            QueryCondition.MayOrMayNotExist,
            TestContext.Current.CancellationToken
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLatestEntityItemAsync_Returns_The_Users_Most_Recent_Request()
    {
        var context = AppDbContextTestFactory.GetAppDbContext();
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
        var repository = new PasswordResetRepository(AppDbContextTestFactory.GetAppDbContext());

        var result = await repository.GetLatestEntityItemAsync(Guid.NewGuid().ToString(), TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task CountEntityItemsCreatedAfterAsync_Counts_Only_The_Users_Recent_Requests()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var context = AppDbContextTestFactory.GetAppDbContext();
        var recent = BuildPasswordReset();
        recent.CreatedAt = now.AddHours(-1);
        var alsoRecent = BuildPasswordReset();
        alsoRecent.UserId = recent.UserId;
        alsoRecent.CreatedAt = now.AddHours(-2);
        var old = BuildPasswordReset();
        old.UserId = recent.UserId;
        old.CreatedAt = now.AddDays(-2);
        var otherUser = BuildPasswordReset();
        otherUser.CreatedAt = now.AddHours(-1);
        context.PasswordReset.AddRange(recent, alsoRecent, old, otherUser);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var count = await new PasswordResetRepository(context).CountEntityItemsCreatedAfterAsync(
            recent.UserId,
            now.AddDays(-1),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(2, count);
    }

    private static PasswordResetEntity BuildPasswordReset()
    {
        return new PasswordResetEntity
        {
            Id = Guid.NewGuid(),
            RequestId = Guid.NewGuid().ToString(),
            UserId = Guid.NewGuid().ToString(),
            Expires = DateTime.UtcNow.AddMinutes(15),
            CreatedAt = DateTime.UtcNow,
        };
    }
}
