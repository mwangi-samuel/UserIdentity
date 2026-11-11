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
        // Arrange
        var context = AppDbContextTestFactory.GetAppDbContext();
        var repository = new PasswordResetRepository(context);
        var passwordReset = BuildPasswordReset();

        // Act
        repository.CreateEntityItem(passwordReset);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var saved = await context.PasswordReset.SingleOrDefaultAsync(
            x => x.Id == passwordReset.Id,
            cancellationToken: TestContext.Current.CancellationToken
        );
        Assert.Equal(passwordReset.RequestId, saved!.RequestId);
    }

    [Fact]
    public async Task GetEntityByAlternateIdAsync_MustExist_Returns_Entity()
    {
        // Arrange
        var context = AppDbContextTestFactory.GetAppDbContext();
        var repository = new PasswordResetRepository(context);
        var passwordReset = BuildPasswordReset();

        context.PasswordReset.Add(passwordReset);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await repository.GetEntityByAlternateIdAsync(
            new PasswordResetEntity { RequestId = passwordReset.RequestId },
            QueryCondition.MustExist,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(passwordReset.Id, result!.Id);
    }

    [Fact]
    public async Task GetEntityByAlternateIdAsync_MustExist_When_Missing_Throws_NoRecordException()
    {
        // Arrange
        var repository = new PasswordResetRepository(AppDbContextTestFactory.GetAppDbContext());

        // Act & Assert
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
        // Arrange
        var repository = new PasswordResetRepository(AppDbContextTestFactory.GetAppDbContext());

        // Act
        var result = await repository.GetEntityByAlternateIdAsync(
            new PasswordResetEntity { RequestId = Guid.NewGuid().ToString() },
            QueryCondition.MayOrMayNotExist,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetRequestHistoryAsync_Returns_The_Users_Latest_Request_Time_And_Counts_Only_Their_Recent_Requests()
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
        otherUser.CreatedAt = now.AddMinutes(-1);
        context.PasswordReset.AddRange(recent, alsoRecent, old, otherUser);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        (var latestCreatedAt, var countCreatedAfter) = await new PasswordResetRepository(context).GetRequestHistoryAsync(
            recent.UserId,
            now.AddDays(-1),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(recent.CreatedAt, latestCreatedAt);
        Assert.Equal(2, countCreatedAfter);
    }

    [Fact]
    public async Task GetRequestHistoryAsync_When_User_Has_No_Requests_Returns_No_Time_And_No_Count()
    {
        // Arrange
        var repository = new PasswordResetRepository(AppDbContextTestFactory.GetAppDbContext());

        // Act
        (var latestCreatedAt, var countCreatedAfter) = await repository.GetRequestHistoryAsync(
            Guid.NewGuid().ToString(),
            DateTime.UtcNow.AddDays(-1),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(latestCreatedAt);
        Assert.Equal(0, countCreatedAfter);
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
