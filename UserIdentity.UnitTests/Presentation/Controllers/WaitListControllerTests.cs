using System.Net;
using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.AspNetCore.Mvc;

using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Resolvers;
using PolyzenKit.Application.Enums;
using PolyzenKit.Domain.RegisteredApps;

using UserIdentity.Application.Core.WaitLists.Commands;
using UserIdentity.Application.Core.WaitLists.Queries;
using UserIdentity.Application.Core.WaitLists.ViewModels;
using UserIdentity.Domain.WaitLists;
using UserIdentity.Presentation.Controllers;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Presentation.Controllers;

public class WaitListControllerTests
{
  private static readonly string ControllerName = "waitlist";

  private readonly ICreateItemCommandHandler<CreateWaitListCommand, WaitListViewModel> _createWaitListCommandHandler;
  private readonly IGetItemQueryHandler<GetWaitListQuery, WaitListViewModel> _getWaitListQueryHandler;
  private readonly IRegisteredAppResolver _registeredAppResolver;

  public WaitListControllerTests()
  {
    _createWaitListCommandHandler = A.Fake<ICreateItemCommandHandler<CreateWaitListCommand, WaitListViewModel>>();
    _getWaitListQueryHandler = A.Fake<IGetItemQueryHandler<GetWaitListQuery, WaitListViewModel>>();
    _registeredAppResolver = A.Fake<IRegisteredAppResolver>();
  }

  [Fact]
  public async Task GetWaitList_Returns_WaitList()
  {
    // Arrange
    var waitListId = 1L;
    var waitListVM = new WaitListViewModel
    {
      WaitList = new WaitListDTO
      {
        Id = waitListId,
        UserEmail = "test@example.com",
        AppId = 1,
        AppName = "TestApp"
      }
    };

    A.CallTo(() => _getWaitListQueryHandler.GetItemAsync(A<GetWaitListQuery>.That.Matches(q => q.WaitListId == waitListId)))
        .Returns(waitListVM);

    var controller = GetWaitListController();
    controller.UpdateContext(ControllerName);

    // Act
    var actionResult = await controller.GetWaitList(waitListId);
    var result = actionResult?.Result as ObjectResult;
    var vm = result?.Value as WaitListViewModel;

    // Assert
    Assert.Equal((int)HttpStatusCode.OK, result?.StatusCode);
    Assert.NotNull(vm);
    Assert.Equal(waitListVM.WaitList.Id, vm?.WaitList.Id);
    Assert.Equal(waitListVM.WaitList.UserEmail, vm?.WaitList.UserEmail);

    Assert.Contains(RequestStatus.SUCCESSFUL.Description(), vm?.RequestStatus);
    Assert.Contains(ItemStatusMessage.FETCH_ITEM_SUCCESSFUL.Description(), vm?.StatusMessage);
  }

  [Fact]
  public async Task GetWaitListByEmail_Returns_WaitList()
  {
    // Arrange
    var userEmail = "test@example.com";
    var waitListVM = new WaitListViewModel
    {
      WaitList = new WaitListDTO
      {
        Id = 1,
        UserEmail = userEmail,
        AppId = 1,
        AppName = "TestApp"
      }
    };

    A.CallTo(() => _getWaitListQueryHandler.GetItemAsync(A<GetWaitListQuery>.That.Matches(q => q.UserEmail == userEmail)))
        .Returns(waitListVM);

    var controller = GetWaitListController();
    controller.UpdateContext(ControllerName);

    // Act
    var actionResult = await controller.GetWaitListByEmail(userEmail);
    var result = actionResult?.Result as ObjectResult;
    var vm = result?.Value as WaitListViewModel;

    // Assert
    Assert.Equal((int)HttpStatusCode.OK, result?.StatusCode);
    Assert.NotNull(vm);
    Assert.Equal(waitListVM.WaitList.UserEmail, vm?.WaitList.UserEmail);

    Assert.Contains(RequestStatus.SUCCESSFUL.Description(), vm?.RequestStatus);
    Assert.Contains(ItemStatusMessage.FETCH_ITEM_SUCCESSFUL.Description(), vm?.StatusMessage);
  }

  [Fact]
  public async Task CreateWaitList_Creates_And_Returns_WaitList()
  {
    // Arrange
    var appId = 1;
    var appName = "TestApp";

    var command = new CreateWaitListCommand
    {
      UserEmail = "new@example.com"
    };

    var registeredApp = new RegisteredAppEntity
    {
      Id = appId,
      AppName = appName
    };

    var waitListVM = new WaitListViewModel
    {
      WaitList = new WaitListDTO
      {
        Id = 1,
        UserEmail = command.UserEmail,
        AppId = appId,
        AppName = appName
      }
    };

    A.CallTo(() => _registeredAppResolver.GetActiveAppAsync(A<string>._, A<CancellationToken>._))
        .Returns(registeredApp);
    A.CallTo(() => _createWaitListCommandHandler.CreateItemAsync(A<CreateWaitListCommand>._, TestStringHelper.UserId))
        .Returns(waitListVM);

    var controller = GetWaitListController();
    controller.UpdateContext(ControllerName, addUserId: true, userId: TestStringHelper.UserId, appName: appName);

    // Act
    var actionResult = await controller.CreateWaitList(command);
    var result = actionResult?.Result as ObjectResult;
    var vm = result?.Value as WaitListViewModel;

    // Assert
    Assert.Equal((int)HttpStatusCode.Created, result?.StatusCode);
    Assert.NotNull(vm);
    Assert.Equal(waitListVM.WaitList.UserEmail, vm?.WaitList.UserEmail);
    Assert.Equal(appId, vm?.WaitList.AppId);

    Assert.Contains(RequestStatus.SUCCESSFUL.Description(), vm?.RequestStatus);
    Assert.Contains(ItemStatusMessage.CREATE_ITEM_SUCCESSFUL.Description(), vm?.StatusMessage);

    A.CallTo(() => _registeredAppResolver.GetActiveAppAsync(A<string>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _createWaitListCommandHandler.CreateItemAsync(A<CreateWaitListCommand>._, TestStringHelper.UserId)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task CreateWaitList_Without_UserId_Uses_Anonymous()
  {
    // Arrange
    var appId = 1;
    var appName = "TestApp";

    var command = new CreateWaitListCommand
    {
      UserEmail = "anonymous@example.com"
    };

    var registeredApp = new RegisteredAppEntity
    {
      Id = appId,
      AppName = appName
    };

    var waitListVM = new WaitListViewModel
    {
      WaitList = new WaitListDTO
      {
        Id = 1,
        UserEmail = command.UserEmail,
        AppId = appId,
        AppName = appName
      }
    };

    A.CallTo(() => _registeredAppResolver.GetActiveAppAsync(A<string>._, A<CancellationToken>._))
        .Returns(registeredApp);
    A.CallTo(() => _createWaitListCommandHandler.CreateItemAsync(A<CreateWaitListCommand>._, A<string>._))
        .Returns(waitListVM);

    var controller = GetWaitListController();
    controller.UpdateContext(ControllerName, appName: appName);

    // Act
    var actionResult = await controller.CreateWaitList(command);
    var result = actionResult?.Result as ObjectResult;
    var vm = result?.Value as WaitListViewModel;

    // Assert
    Assert.Equal((int)HttpStatusCode.Created, result?.StatusCode);
    Assert.NotNull(vm);

    A.CallTo(() => _createWaitListCommandHandler.CreateItemAsync(A<CreateWaitListCommand>._, A<string>._)).MustHaveHappenedOnceExactly();
  }

  private WaitListController GetWaitListController()
  {
    return new WaitListController(
        _createWaitListCommandHandler,
        _getWaitListQueryHandler,
        _registeredAppResolver);
  }
}
