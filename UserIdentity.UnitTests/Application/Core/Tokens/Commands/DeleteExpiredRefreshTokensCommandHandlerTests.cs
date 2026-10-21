using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using PolyzenKit.Application.Interfaces;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Domain.RefreshTokens;
using UserIdentity.Persistence.Repositories.RefreshTokens;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Tokens.Commands;

public class DeleteExpiredRefreshTokensCommandHandlerTests
{
  [Fact]
  public async Task DeleteItemAsync_KeepsReadingBatches_UntilOneIsShort()
  {
    // Arrange
    var now = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
    var repository = A.Fake<IRefreshTokenRepository>();
    var unitOfWork = A.Fake<IUnitOfWork>();
    var machineDateTime = A.Fake<IMachineDateTime>();
    A.CallTo(() => machineDateTime.Now).Returns(now);

    static List<RefreshTokenEntity> Build(int count) => [.. Enumerable.Range(0, count).Select(_ => new RefreshTokenEntity())];

    A.CallTo(() => repository.GetEntityItemsExpiredBeforeAsync(now, DeleteExpiredRefreshTokensCommandHandler.BatchSize, A<CancellationToken>._))
      .ReturnsNextFromSequence(Build(DeleteExpiredRefreshTokensCommandHandler.BatchSize), Build(1));

    var handler = new DeleteExpiredRefreshTokensCommandHandler(repository, unitOfWork, machineDateTime);

    // Act
    await handler.DeleteItemAsync(new DeleteExpiredRefreshTokensCommand(), "system", TestContext.Current.CancellationToken);

    // Assert
    A.CallTo(() => repository.RemoveEntityItem(A<RefreshTokenEntity>._)).MustHaveHappened(DeleteExpiredRefreshTokensCommandHandler.BatchSize + 1, Times.Exactly);
    A.CallTo(() => unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
  }
}
