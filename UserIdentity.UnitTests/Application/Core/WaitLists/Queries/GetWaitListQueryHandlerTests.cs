using FakeItEasy;

using PolyzenKit.Common.Exceptions;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.WaitLists.Queries;
using UserIdentity.Domain.WaitLists;
using UserIdentity.Persistence.Repositories.WaitLists;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.WaitLists.Queries;

public class GetWaitListQueryHandlerTests
{
  private readonly IWaitListRepository _waitListRepository = A.Fake<IWaitListRepository>();

  [Fact]
  public async Task GetItemAsync_With_An_Id_Returns_The_Apps_Entry()
  {
    // Arrange
    var dto = new WaitListDto { Id = 5, UserEmail = "shopper@example.com", AppId = 1, AppName = "TestApp" };
    A.CallTo(() => _waitListRepository.GetEntityDtoByIdAsync(5, A<CancellationToken>._)).Returns(dto);

    // Act
    var vm = await new GetWaitListQueryHandler(_waitListRepository).GetItemAsync(new GetWaitListQuery { WaitListId = 5, AppId = 1 }, TestContext.Current.CancellationToken);

    // Assert
    Assert.Same(dto, vm.WaitList);
    A.CallTo(() => _waitListRepository.GetEntityByAlternateIdAsync(A<WaitListEntity>._, A<QueryCondition>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task GetItemAsync_With_An_Email_Reads_The_Apps_Entry_For_That_Email()
  {
    // Arrange
    var entity = new WaitListEntity { Id = 7, UserEmail = "shopper@example.com", AppId = 1, App = new RegisteredAppEntity { Id = 1, AppName = "TestApp" } };
    A.CallTo(() => _waitListRepository.GetEntityByAlternateIdAsync(
        A<WaitListEntity>.That.Matches(e => e.AppId == 1 && e.UserEmail == "shopper@example.com"), QueryCondition.MustExist, A<CancellationToken>._))
      .Returns(entity);

    // Act
    var vm = await new GetWaitListQueryHandler(_waitListRepository).GetItemAsync(new GetWaitListQuery { UserEmail = "shopper@example.com", AppId = 1 }, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(7, vm.WaitList.Id);
    Assert.Equal("shopper@example.com", vm.WaitList.UserEmail);
    Assert.Equal("TestApp", vm.WaitList.AppName);
  }

  [Fact]
  public async Task GetItemAsync_For_Another_Apps_Entry_Throws_NoRecordException()
  {
    // Arrange
    A.CallTo(() => _waitListRepository.GetEntityDtoByIdAsync(5, A<CancellationToken>._)).Returns(new WaitListDto { Id = 5, UserEmail = "shopper@example.com", AppId = 2 });

    // Act & Assert
    var exception = await Assert.ThrowsAsync<NoRecordException>(() => new GetWaitListQueryHandler(_waitListRepository).GetItemAsync(new GetWaitListQuery { WaitListId = 5, AppId = 1 }, TestContext.Current.CancellationToken));

    Assert.Equal("No record exists for the provided identifier - 5", exception.Message);
  }
}
