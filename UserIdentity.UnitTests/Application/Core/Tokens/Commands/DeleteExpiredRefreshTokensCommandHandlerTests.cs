using System;
using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using PolyzenKit.Application.Interfaces;

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
    var machineDateTime = A.Fake<IMachineDateTime>();
    A.CallTo(() => machineDateTime.Now).Returns(now);

    A.CallTo(() => repository.DeleteEntityItemsExpiredBeforeAsync(now, DeleteExpiredRefreshTokensCommandHandler.BatchSize, A<CancellationToken>._))
      .ReturnsNextFromSequence(DeleteExpiredRefreshTokensCommandHandler.BatchSize, DeleteExpiredRefreshTokensCommandHandler.BatchSize, 3);

    var handler = new DeleteExpiredRefreshTokensCommandHandler(repository, machineDateTime);

    // Act
    await handler.DeleteItemAsync(new DeleteExpiredRefreshTokensCommand(), "system", TestContext.Current.CancellationToken);

    // Assert
    A.CallTo(() => repository.DeleteEntityItemsExpiredBeforeAsync(now, DeleteExpiredRefreshTokensCommandHandler.BatchSize, A<CancellationToken>._))
      .MustHaveHappened(3, Times.Exactly);
  }
}
