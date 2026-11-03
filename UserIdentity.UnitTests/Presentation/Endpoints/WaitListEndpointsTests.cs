using System.Net;

using FakeItEasy;

using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Common.Enums;
using PolyzenKit.Domain.Entities.RegisteredApps;

using UserIdentity.Application.Core.WaitLists.Commands;
using UserIdentity.Application.Core.WaitLists.Queries;
using UserIdentity.Application.Core.WaitLists.ViewModels;
using UserIdentity.Domain.WaitLists;
using UserIdentity.Presentation.Endpoints;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Presentation.Endpoints;

public class WaitListEndpointsTests
{
  private readonly ICreateItemCommandHandler<CreateWaitListCommand, WaitListViewModel> _createWaitListCommandHandler;
  private readonly IGetItemQueryHandler<GetWaitListQuery, WaitListViewModel> _getWaitListQueryHandler;
  private readonly IRegisteredAppResolver _registeredAppResolver;

  public WaitListEndpointsTests()
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
      WaitList = new WaitListDto
      {
        Id = waitListId,
        UserEmail = "test@example.com",
        AppId = 1,
        AppName = "TestApp"
      }
    };

    A.CallTo(() => _getWaitListQueryHandler.GetItemAsync(
      A<GetWaitListQuery>.That.Matches(q => q.WaitListId == waitListId),
      A<CancellationToken>._))
        .Returns(waitListVM);

    var request = ApiRequestFactory.Create("wait-list");

    // Act
    var result = await WaitListEndpoints.GetWaitList(
      waitListId,
      request,
      _registeredAppResolver,
      _getWaitListQueryHandler,
      TestContext.Current.CancellationToken);
    var vm = result.Value;

    // Assert
    Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
    Assert.NotNull(vm);
    Assert.Equal(waitListVM.WaitList.Id, vm?.WaitList.Id);
    Assert.Equal(waitListVM.WaitList.UserEmail, vm?.WaitList.UserEmail);

    Assert.Contains(RequestStatus.Successful.ToText(), vm?.RequestStatus);
    Assert.Contains(ItemStatusMessage.FetchItemSuccessful.ToText(), vm?.StatusMessage);
  }

  [Fact]
  public async Task GetWaitListByEmail_Returns_WaitList()
  {
    // Arrange
    var userEmail = "test@example.com";
    var waitListVM = new WaitListViewModel
    {
      WaitList = new WaitListDto
      {
        Id = 1,
        UserEmail = userEmail,
        AppId = 1,
        AppName = "TestApp"
      }
    };

    A.CallTo(() => _getWaitListQueryHandler.GetItemAsync(
      A<GetWaitListQuery>.That.Matches(q => q.UserEmail == userEmail),
      A<CancellationToken>._))
        .Returns(waitListVM);

    var request = ApiRequestFactory.Create("wait-list");

    // Act
    var result = await WaitListEndpoints.GetWaitListByEmail(
      userEmail,
      request,
      _registeredAppResolver,
      _getWaitListQueryHandler,
      TestContext.Current.CancellationToken);
    var vm = result.Value;

    // Assert
    Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
    Assert.NotNull(vm);
    Assert.Equal(waitListVM.WaitList.UserEmail, vm?.WaitList.UserEmail);

    Assert.Contains(RequestStatus.Successful.ToText(), vm?.RequestStatus);
    Assert.Contains(ItemStatusMessage.FetchItemSuccessful.ToText(), vm?.StatusMessage);
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
      WaitList = new WaitListDto
      {
        Id = 1,
        UserEmail = command.UserEmail,
        AppId = appId,
        AppName = appName
      }
    };

    A.CallTo(() => _registeredAppResolver.GetActiveAppAsync(A<string>._, A<CancellationToken>._))
        .Returns(registeredApp);
    A.CallTo(() => _createWaitListCommandHandler.CreateItemAsync(
      A<CreateWaitListCommand>._,
      TestStringHelper.UserId,
      A<CancellationToken>._))
        .Returns(waitListVM);

    var request = ApiRequestFactory.Create("wait-list", userId: TestStringHelper.UserId, appName: appName);

    // Act
    var result = await WaitListEndpoints.CreateWaitList(
      command,
      request,
      _registeredAppResolver,
      _createWaitListCommandHandler,
      TestContext.Current.CancellationToken);
    var vm = result.Value;

    // Assert
    Assert.Equal((int)HttpStatusCode.Created, result.StatusCode);
    Assert.NotNull(vm);
    Assert.Equal(waitListVM.WaitList.UserEmail, vm?.WaitList.UserEmail);
    Assert.Equal(appId, vm?.WaitList.AppId);

    Assert.Contains(RequestStatus.Successful.ToText(), vm?.RequestStatus);
    Assert.Contains(ItemStatusMessage.CreateItemSuccessful.ToText(), vm?.StatusMessage);

    A.CallTo(() => _registeredAppResolver.GetActiveAppAsync(A<string>._, A<CancellationToken>._))
      .MustHaveHappenedOnceExactly();
    A.CallTo(() => _createWaitListCommandHandler.CreateItemAsync(
      A<CreateWaitListCommand>._,
      TestStringHelper.UserId,
      A<CancellationToken>._))
      .MustHaveHappenedOnceExactly();
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
      WaitList = new WaitListDto
      {
        Id = 1,
        UserEmail = command.UserEmail,
        AppId = appId,
        AppName = appName
      }
    };

    A.CallTo(() => _registeredAppResolver.GetActiveAppAsync(A<string>._, A<CancellationToken>._))
        .Returns(registeredApp);
    A.CallTo(() => _createWaitListCommandHandler.CreateItemAsync(
      A<CreateWaitListCommand>._,
      A<string>._,
      A<CancellationToken>._))
        .Returns(waitListVM);

    var request = ApiRequestFactory.Create("wait-list", appName: appName);

    // Act
    var result = await WaitListEndpoints.CreateWaitList(
      command,
      request,
      _registeredAppResolver,
      _createWaitListCommandHandler,
      TestContext.Current.CancellationToken);
    var vm = result.Value;

    // Assert
    Assert.Equal((int)HttpStatusCode.Created, result.StatusCode);
    Assert.NotNull(vm);

    A.CallTo(() => _createWaitListCommandHandler.CreateItemAsync(
      A<CreateWaitListCommand>._,
      A<string>._,
      A<CancellationToken>._))
      .MustHaveHappenedOnceExactly();
  }
}
