using PolyzenKit.Persistence.Repositories;
using UserIdentity.Domain.StepUps;
using UserIdentity.Persistence.Repositories.StepUps;
using Xunit;

namespace UserIdentity.UnitTests.Persistence.Repositories.StepUps;

public class StepUpRepositoryTests
{
    private static readonly DateTime _now = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetPendingEntityAsync_Returns_The_Newest_Unexpired_Step_Up_Still_Waiting_For_Its_Code()
    {
        // Arrange
        var context = AppDbContextTestFactory.GetAppDbContext();
        var repository = new StepUpRepository(context);
        var newest = BuildStepUp(createdAt: _now.AddMinutes(-1), expires: _now.AddMinutes(9));

        context.StepUp.AddRange(
            BuildStepUp(createdAt: _now.AddMinutes(-5), expires: _now.AddMinutes(5)),
            newest,
            BuildStepUp(createdAt: _now, expires: _now.AddMinutes(5), ticket: "hashed-ticket"),
            BuildStepUp(createdAt: _now, expires: _now),
            BuildStepUp(createdAt: _now, expires: _now.AddMinutes(5), userId: "other-admin"),
            BuildStepUp(createdAt: _now, expires: _now.AddMinutes(5), appId: 2)
        );
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await repository.GetPendingEntityAsync("admin-id", 1, _now, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(newest.Id, result?.Id);
    }

    [Fact]
    public async Task GetPendingEntityAsync_Without_An_Unexpired_One_Returns_Null()
    {
        // Arrange
        var context = AppDbContextTestFactory.GetAppDbContext();
        var repository = new StepUpRepository(context);

        context.StepUp.Add(BuildStepUp(createdAt: _now.AddMinutes(-10), expires: _now.AddSeconds(-1)));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await repository.GetPendingEntityAsync("admin-id", 1, _now, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetEntityByAlternateIdAsync_Finds_A_Ticket_Only_In_Its_App()
    {
        // Arrange
        var context = AppDbContextTestFactory.GetAppDbContext();
        var repository = new StepUpRepository(context);
        var stepUp = BuildStepUp(createdAt: _now, expires: _now.AddMinutes(5), ticket: "hashed-ticket");

        context.StepUp.Add(stepUp);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var found = await repository.GetEntityByAlternateIdAsync(
            new StepUpEntity { Ticket = "hashed-ticket", AppId = 1 },
            QueryCondition.MayOrMayNotExist,
            TestContext.Current.CancellationToken
        );
        var otherApp = await repository.GetEntityByAlternateIdAsync(
            new StepUpEntity { Ticket = "hashed-ticket", AppId = 2 },
            QueryCondition.MayOrMayNotExist,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(stepUp.Id, found?.Id);
        Assert.Null(otherApp);
    }

    private static StepUpEntity BuildStepUp(
        DateTime createdAt,
        DateTime expires,
        string? ticket = null,
        string userId = "admin-id",
        int appId = 1
    ) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            AppId = appId,
            Ticket = ticket,
            Expires = expires,
            CreatedBy = userId,
            CreatedAt = createdAt,
        };
}
