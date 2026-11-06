using FakeItEasy;
using Microsoft.Extensions.Time.Testing;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Tokens.Commands;

public class DeleteExpiredRefreshTokensCommandHandlerTests
{
    [Fact]
    public async Task DeleteItemAsync_Keeps_Deleting_Batches_Until_One_Is_Short()
    {
        // Arrange
        var now = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        var repository = A.Fake<IRefreshTokenRepository>();
        var timeProvider = new FakeTimeProvider();
        timeProvider.SetUtcNow(now);

        A.CallTo(() =>
                repository.DeleteEntityItemsExpiredBeforeAsync(
                    now,
                    DeleteExpiredRefreshTokensCommandHandler.BatchSize,
                    A<CancellationToken>._
                )
            )
            .ReturnsNextFromSequence(
                DeleteExpiredRefreshTokensCommandHandler.BatchSize,
                DeleteExpiredRefreshTokensCommandHandler.BatchSize,
                3
            );

        var handler = new DeleteExpiredRefreshTokensCommandHandler(repository, timeProvider);

        // Act
        await handler.DeleteItemAsync(new DeleteExpiredRefreshTokensCommand(), "system", TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() =>
                repository.DeleteEntityItemsExpiredBeforeAsync(
                    now,
                    DeleteExpiredRefreshTokensCommandHandler.BatchSize,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappened(3, Times.Exactly);
    }
}
