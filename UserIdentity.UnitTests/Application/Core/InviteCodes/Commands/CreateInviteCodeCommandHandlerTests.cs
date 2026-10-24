using System;
using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using PolyzenKit.Application.Interfaces;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.InviteCodes.Commands;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Persistence.Repositories.InviteCodes;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.InviteCodes.Commands;

public class CreateInviteCodeCommandHandlerTests
{
  private readonly IUnitOfWork _unitOfWork;
  private readonly IInviteCodeRepository _inviteCodeRepository;
  private readonly IMachineDateTime _machineDateTime;

  public CreateInviteCodeCommandHandlerTests()
  {
    _unitOfWork = A.Fake<IUnitOfWork>();
    _inviteCodeRepository = A.Fake<IInviteCodeRepository>();
    _machineDateTime = A.Fake<IMachineDateTime>();

    A.CallTo(() => _machineDateTime.Now).Returns(DateTime.UtcNow);
  }

  [Fact]
  public async Task CreateItemAsync_Creates_InviteCode_And_Returns_ViewModel()
  {
    // Arrange
    var command = new CreateInviteCodeCommand
    {
      InviteCode = "INVITE123",
      UserEmail = "test@example.com",
      AppId = 1,
      Applied = false
    };

    var expectedDto = new InviteCodeDto
    {
      Id = 1,
      InviteCode = command.InviteCode,
      UserEmail = command.UserEmail,
      AppId = command.AppId,
      Applied = command.Applied,
      AppName = "TestApp"
    };

    A.CallTo(() => _inviteCodeRepository.GetEntityDtoByIdAsync(A<long>._, A<CancellationToken>._)).Returns(expectedDto);

    var handler = new CreateInviteCodeCommandHandler(_unitOfWork, _inviteCodeRepository, _machineDateTime);

    // Act
    var result = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.NotNull(result);
    Assert.NotNull(result.InviteCode);
    Assert.Equal(expectedDto.InviteCode, result.InviteCode.InviteCode);
    Assert.Equal(expectedDto.UserEmail, result.InviteCode.UserEmail);
    Assert.Equal(expectedDto.AppId, result.InviteCode.AppId);

    A.CallTo(() => _inviteCodeRepository.CreateEntityItem(A<InviteCodeEntity>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _inviteCodeRepository.GetEntityDtoByIdAsync(A<long>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task CreateItemAsync_Sets_Audit_Fields()
  {
    // Arrange
    var now = DateTime.UtcNow;
    A.CallTo(() => _machineDateTime.Now).Returns(now);

    var command = new CreateInviteCodeCommand
    {
      InviteCode = "INVITE456",
      UserEmail = "audit@example.com",
      AppId = 2
    };

    InviteCodeEntity? capturedEntity = null;
    A.CallTo(() => _inviteCodeRepository.CreateEntityItem(A<InviteCodeEntity>._))
        .Invokes((InviteCodeEntity e) => capturedEntity = e);

    A.CallTo(() => _inviteCodeRepository.GetEntityDtoByIdAsync(A<long>._, A<CancellationToken>._)).Returns(new InviteCodeDto
    {
      Id = 1,
      InviteCode = command.InviteCode,
      UserEmail = command.UserEmail,
      AppId = command.AppId
    });

    var handler = new CreateInviteCodeCommandHandler(_unitOfWork, _inviteCodeRepository, _machineDateTime);

    // Act
    await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.NotNull(capturedEntity);
    Assert.Equal(TestStringHelper.UserId, capturedEntity!.CreatedBy);
    Assert.Equal(TestStringHelper.UserId, capturedEntity.UpdatedBy);
  }
}
