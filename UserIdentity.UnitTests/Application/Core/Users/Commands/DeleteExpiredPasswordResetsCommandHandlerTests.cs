using FakeItEasy;
using Microsoft.Extensions.Time.Testing;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Persistence.Repositories.PasswordResets;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class DeleteExpiredPasswordResetsCommandHandlerTests
{
    [Fact]
    public async Task DeleteItemAsync_Deletes_Requests_That_Expired_Before_The_Count_Window_In_Batches()
    {
        // Arrange
        var now = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        var expiredBefore = now.AddDays(-1);
        var repository = A.Fake<IPasswordResetRepository>();
        var timeProvider = new FakeTimeProvider();
        timeProvider.SetUtcNow(now);

        A.CallTo(() => repository.DeleteEntityItemsExpiredBeforeAsync(expiredBefore, CleanupBatch.Size, A<CancellationToken>._))
            .ReturnsNextFromSequence(CleanupBatch.Size, 0);

        var handler = new DeleteExpiredPasswordResetsCommandHandler(repository, timeProvider);

        // Act
        await handler.DeleteItemAsync(new DeleteExpiredPasswordResetsCommand(), "system", TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => repository.DeleteEntityItemsExpiredBeforeAsync(expiredBefore, CleanupBatch.Size, A<CancellationToken>._))
            .MustHaveHappenedTwiceExactly();
    }
}
