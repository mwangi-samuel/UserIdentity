using System;
using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using PolyzenKit.Application.Interfaces;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.WaitLists.Commands;
using UserIdentity.Domain.WaitLists;
using UserIdentity.Persistence.Repositories.WaitLists;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.WaitLists.Commands;

public class CreateWaitListCommandHandlerTests
{
  private readonly IUnitOfWork _unitOfWork;
  private readonly IWaitListRepository _waitListRepository;
  private readonly IMachineDateTime _machineDateTime;

  public CreateWaitListCommandHandlerTests()
  {
    _unitOfWork = A.Fake<IUnitOfWork>();
    _waitListRepository = A.Fake<IWaitListRepository>();
    _machineDateTime = A.Fake<IMachineDateTime>();

    A.CallTo(() => _machineDateTime.Now).Returns(DateTime.UtcNow);
  }

  [Fact]
  public async Task CreateItemAsync_Creates_WaitList_And_Returns_ViewModel()
  {
    // Arrange
    var command = new CreateWaitListCommand
    {
      UserEmail = "waitlist@example.com",
      AppId = 1
    };

    var expectedDto = new WaitListDto
    {
      Id = 1,
      UserEmail = command.UserEmail,
      AppId = command.AppId,
      AppName = "TestApp"
    };

    A.CallTo(() => _waitListRepository.GetEntityDtoByIdAsync(A<long>._, A<CancellationToken>._)).Returns(expectedDto);

    var handler = new CreateWaitListCommandHandler(_unitOfWork, _waitListRepository, _machineDateTime);

    // Act
    var result = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.NotNull(result);
    Assert.NotNull(result.WaitList);
    Assert.Equal(expectedDto.UserEmail, result.WaitList.UserEmail);
    Assert.Equal(expectedDto.AppId, result.WaitList.AppId);
    Assert.Equal(expectedDto.AppName, result.WaitList.AppName);

    A.CallTo(() => _waitListRepository.CreateEntityItem(A<WaitListEntity>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _waitListRepository.GetEntityDtoByIdAsync(A<long>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task CreateItemAsync_Sets_Audit_Fields()
  {
    // Arrange
    var now = DateTime.UtcNow;
    A.CallTo(() => _machineDateTime.Now).Returns(now);

    var command = new CreateWaitListCommand
    {
      UserEmail = "audit@example.com",
      AppId = 2
    };

    WaitListEntity? capturedEntity = null;
    A.CallTo(() => _waitListRepository.CreateEntityItem(A<WaitListEntity>._))
        .Invokes((WaitListEntity e) => capturedEntity = e);

    A.CallTo(() => _waitListRepository.GetEntityDtoByIdAsync(A<long>._, A<CancellationToken>._)).Returns(new WaitListDto
    {
      Id = 1,
      UserEmail = command.UserEmail,
      AppId = command.AppId
    });

    var handler = new CreateWaitListCommandHandler(_unitOfWork, _waitListRepository, _machineDateTime);

    // Act
    await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.NotNull(capturedEntity);
    Assert.Equal(TestStringHelper.UserId, capturedEntity!.CreatedBy);
    Assert.Equal(TestStringHelper.UserId, capturedEntity.UpdatedBy);
  }
}
