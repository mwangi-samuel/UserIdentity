using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using PolyzenKit.Common.Exceptions;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.InviteCodes.Commands;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Persistence.Repositories.InviteCodes;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.InviteCodes.Commands;

public class DeleteInviteCodeCommandHandlerTests
{
  private readonly IUnitOfWork _unitOfWork = A.Fake<IUnitOfWork>();
  private readonly IInviteCodeRepository _inviteCodeRepository = A.Fake<IInviteCodeRepository>();

  [Fact]
  public async Task DeleteItemAsync_Removes_An_Unused_InviteCode_Of_The_App()
  {
    // Arrange
    var entity = new InviteCodeEntity { Id = 5, InviteCode = "INVITE", UserEmail = "owner@example.com", AppId = 1, Applied = false };

    A.CallTo(() => _inviteCodeRepository.GetEntityItemAsync(5, A<CancellationToken>._)).Returns(entity);

    var handler = new DeleteInviteCodeCommandHandler(_unitOfWork, _inviteCodeRepository);

    // Act
    var result = await handler.DeleteItemAsync(new DeleteInviteCodeCommand { InviteCodeId = 5, AppId = 1 }, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.NotNull(result);
    A.CallTo(() => _inviteCodeRepository.RemoveEntityItem(entity)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task DeleteItemAsync_For_A_Used_InviteCode_Throws_InvalidDataException()
  {
    // Arrange
    var entity = new InviteCodeEntity { Id = 5, InviteCode = "INVITE", UserEmail = "owner@example.com", AppId = 1, Applied = true };

    A.CallTo(() => _inviteCodeRepository.GetEntityItemAsync(5, A<CancellationToken>._)).Returns(entity);

    var handler = new DeleteInviteCodeCommandHandler(_unitOfWork, _inviteCodeRepository);

    // Act & Assert
    await Assert.ThrowsAsync<InvalidDataException>(() => handler.DeleteItemAsync(new DeleteInviteCodeCommand { InviteCodeId = 5, AppId = 1 }, TestStringHelper.UserId, TestContext.Current.CancellationToken));

    A.CallTo(() => _inviteCodeRepository.RemoveEntityItem(A<InviteCodeEntity>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task DeleteItemAsync_For_Another_Apps_InviteCode_Throws_NoRecordException()
  {
    // Arrange
    var entity = new InviteCodeEntity { Id = 5, InviteCode = "INVITE", UserEmail = "owner@example.com", AppId = 2, Applied = false };

    A.CallTo(() => _inviteCodeRepository.GetEntityItemAsync(5, A<CancellationToken>._)).Returns(entity);

    var handler = new DeleteInviteCodeCommandHandler(_unitOfWork, _inviteCodeRepository);

    // Act & Assert
    await Assert.ThrowsAsync<NoRecordException>(() => handler.DeleteItemAsync(new DeleteInviteCodeCommand { InviteCodeId = 5, AppId = 1 }, TestStringHelper.UserId, TestContext.Current.CancellationToken));

    A.CallTo(() => _inviteCodeRepository.RemoveEntityItem(A<InviteCodeEntity>._)).MustNotHaveHappened();
  }
}
