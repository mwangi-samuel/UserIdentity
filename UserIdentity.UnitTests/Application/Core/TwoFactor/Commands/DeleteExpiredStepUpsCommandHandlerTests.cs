using FakeItEasy;
using Microsoft.Extensions.Time.Testing;
using UserIdentity.Application.Core.TwoFactor.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Persistence.Repositories.StepUps;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.TwoFactor.Commands;

public class DeleteExpiredStepUpsCommandHandlerTests
{
    [Fact]
    public async Task DeleteItemAsync_Keeps_Deleting_Batches_Until_One_Is_Short()
    {
        // Arrange
        var now = new DateTime(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);
        var repository = A.Fake<IStepUpRepository>();
        var timeProvider = new FakeTimeProvider();
        timeProvider.SetUtcNow(now);

        A.CallTo(() => repository.DeleteEntityItemsExpiredBeforeAsync(now, CleanupBatch.Size, A<CancellationToken>._))
            .ReturnsNextFromSequence(CleanupBatch.Size, 3);

        var handler = new DeleteExpiredStepUpsCommandHandler(repository, timeProvider);

        // Act
        await handler.DeleteItemAsync(new DeleteExpiredStepUpsCommand(), "system", TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => repository.DeleteEntityItemsExpiredBeforeAsync(now, CleanupBatch.Size, A<CancellationToken>._))
            .MustHaveHappened(2, Times.Exactly);
    }
}
