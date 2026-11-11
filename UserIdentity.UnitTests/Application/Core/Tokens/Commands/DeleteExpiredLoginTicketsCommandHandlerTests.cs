using FakeItEasy;
using Microsoft.Extensions.Time.Testing;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Persistence.Repositories.LoginTickets;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Tokens.Commands;

public class DeleteExpiredLoginTicketsCommandHandlerTests
{
    [Fact]
    public async Task DeleteItemAsync_Keeps_Deleting_Batches_Until_One_Is_Short()
    {
        // Arrange
        var now = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);
        var repository = A.Fake<ILoginTicketRepository>();
        var timeProvider = new FakeTimeProvider();
        timeProvider.SetUtcNow(now);

        A.CallTo(() => repository.DeleteEntityItemsExpiredBeforeAsync(now, CleanupBatch.Size, A<CancellationToken>._))
            .ReturnsNextFromSequence(CleanupBatch.Size, 3);

        var handler = new DeleteExpiredLoginTicketsCommandHandler(repository, timeProvider);

        // Act
        await handler.DeleteItemAsync(new DeleteExpiredLoginTicketsCommand(), "system", TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => repository.DeleteEntityItemsExpiredBeforeAsync(now, CleanupBatch.Size, A<CancellationToken>._))
            .MustHaveHappened(2, Times.Exactly);
    }
}
