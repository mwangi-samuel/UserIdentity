using System.Net;
using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.AspNetCore.Mvc;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Resolvers;
using PolyzenKit.Application.Enums;
using PolyzenKit.Domain.RegisteredApps;

using UserIdentity.Application.Core.InviteCodes.Commands;
using UserIdentity.Application.Core.InviteCodes.Queries;
using UserIdentity.Application.Core.InviteCodes.ViewModels;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Presentation.Controllers;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Presentation.Controllers;

public class InviteCodeControllerTests
{
  private static readonly string ControllerName = "InviteCode";

  private readonly ICreateItemCommandHandler<CreateInviteCodeCommand, InviteCodeViewModel> _createInviteCodeCommandHandler;
  private readonly IGetItemQueryHandler<GetInviteCodeQuery, InviteCodeViewModel> _getInviteCodeQueryHandler;
  private readonly IGetItemsQueryHandler<GetInviteCodesQuery, InviteCodesViewModel> _getInviteCodesQueryHandler;
  private readonly IDeleteItemCommandHandler<DeleteInviteCodeCommand, DeleteRecordViewModel> _deleteInviteCodeCommandHandler;
  private readonly IRegisteredAppResolver _registeredAppResolver;

  public InviteCodeControllerTests()
  {
    _createInviteCodeCommandHandler = A.Fake<ICreateItemCommandHandler<CreateInviteCodeCommand, InviteCodeViewModel>>();
    _getInviteCodeQueryHandler = A.Fake<IGetItemQueryHandler<GetInviteCodeQuery, InviteCodeViewModel>>();
    _getInviteCodesQueryHandler = A.Fake<IGetItemsQueryHandler<GetInviteCodesQuery, InviteCodesViewModel>>();
    _deleteInviteCodeCommandHandler = A.Fake<IDeleteItemCommandHandler<DeleteInviteCodeCommand, DeleteRecordViewModel>>();
    _registeredAppResolver = A.Fake<IRegisteredAppResolver>();
  }

  [Fact]
  public async Task GetInviteCode_Returns_InviteCode()
  {
    // Arrange
    var inviteCodeId = 1L;
    var inviteCodeVM = new InviteCodeViewModel
    {
      InviteCode = new InviteCodeDto
      {
        Id = inviteCodeId,
        InviteCode = "INVITE123",
        UserEmail = "test@example.com",
        AppId = 1,
        AppName = "TestApp"
      }
    };

    A.CallTo(() => _getInviteCodeQueryHandler.GetItemAsync(A<GetInviteCodeQuery>.That.Matches(q => q.InviteCodeId == inviteCodeId)))
        .Returns(inviteCodeVM);

    var controller = GetInviteCodeController();
    controller.UpdateContext(ControllerName);

    // Act
    var actionResult = await controller.GetInviteCode(inviteCodeId);
    var result = actionResult?.Result as ObjectResult;
    var vm = result?.Value as InviteCodeViewModel;

    // Assert
    Assert.Equal((int)HttpStatusCode.OK, result?.StatusCode);
    Assert.NotNull(vm);
    Assert.Equal(inviteCodeVM.InviteCode.Id, vm?.InviteCode.Id);
    Assert.Equal(inviteCodeVM.InviteCode.InviteCode, vm?.InviteCode.InviteCode);
    Assert.Equal(inviteCodeVM.InviteCode.UserEmail, vm?.InviteCode.UserEmail);

    Assert.Contains(RequestStatus.SUCCESSFUL.Description(), vm?.RequestStatus);
    Assert.Contains(ItemStatusMessage.FETCH_ITEM_SUCCESSFUL.Description(), vm?.StatusMessage);
  }

  [Fact]
  public async Task GetInviteCodeByEmail_Returns_InviteCode()
  {
    // Arrange
    var userEmail = "test@example.com";
    var inviteCodeVM = new InviteCodeViewModel
    {
      InviteCode = new InviteCodeDto
      {
        Id = 1,
        InviteCode = "INVITE456",
        UserEmail = userEmail,
        AppId = 1,
        AppName = "TestApp"
      }
    };

    A.CallTo(() => _getInviteCodeQueryHandler.GetItemAsync(A<GetInviteCodeQuery>.That.Matches(q => q.UserEmail == userEmail)))
        .Returns(inviteCodeVM);

    var controller = GetInviteCodeController();
    controller.UpdateContext(ControllerName);

    // Act
    var actionResult = await controller.GetInviteCodeByEmail(userEmail);
    var result = actionResult?.Result as ObjectResult;
    var vm = result?.Value as InviteCodeViewModel;

    // Assert
    Assert.Equal((int)HttpStatusCode.OK, result?.StatusCode);
    Assert.NotNull(vm);
    Assert.Equal(inviteCodeVM.InviteCode.UserEmail, vm?.InviteCode.UserEmail);
    Assert.Equal(inviteCodeVM.InviteCode.InviteCode, vm?.InviteCode.InviteCode);

    Assert.Contains(RequestStatus.SUCCESSFUL.Description(), vm?.RequestStatus);
    Assert.Contains(ItemStatusMessage.FETCH_ITEM_SUCCESSFUL.Description(), vm?.StatusMessage);
  }

  [Fact]
  public async Task CreateInviteCode_Creates_And_Returns_InviteCode()
  {
    // Arrange
    var appId = 1;
    var appName = "TestApp";

    var command = new CreateInviteCodeCommand
    {
      InviteCode = "NEWINVITE",
      UserEmail = "new@example.com"
    };

    var registeredApp = new RegisteredAppEntity
    {
      Id = appId,
      AppName = appName
    };

    var inviteCodeVM = new InviteCodeViewModel
    {
      InviteCode = new InviteCodeDto
      {
        Id = 1,
        InviteCode = command.InviteCode,
        UserEmail = command.UserEmail,
        AppId = appId,
        AppName = appName
      }
    };

    A.CallTo(() => _registeredAppResolver.GetActiveAppAsync(A<string>._, A<CancellationToken>._))
        .Returns(registeredApp);
    A.CallTo(() => _createInviteCodeCommandHandler.CreateItemAsync(A<CreateInviteCodeCommand>._, TestStringHelper.UserId))
        .Returns(inviteCodeVM);

    var controller = GetInviteCodeController();
    controller.UpdateContext(ControllerName, addUserId: true, userId: TestStringHelper.UserId, appName: appName);

    // Act
    var actionResult = await controller.CreateInviteCode(command);
    var result = actionResult?.Result as ObjectResult;
    var vm = result?.Value as InviteCodeViewModel;

    // Assert
    Assert.Equal((int)HttpStatusCode.Created, result?.StatusCode);
    Assert.NotNull(vm);
    Assert.Equal(inviteCodeVM.InviteCode.InviteCode, vm?.InviteCode.InviteCode);
    Assert.Equal(inviteCodeVM.InviteCode.UserEmail, vm?.InviteCode.UserEmail);
    Assert.Equal(appId, vm?.InviteCode.AppId);

    Assert.Contains(RequestStatus.SUCCESSFUL.Description(), vm?.RequestStatus);
    Assert.Contains(ItemStatusMessage.CREATE_ITEM_SUCCESSFUL.Description(), vm?.StatusMessage);

    A.CallTo(() => _registeredAppResolver.GetActiveAppAsync(A<string>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _createInviteCodeCommandHandler.CreateItemAsync(A<CreateInviteCodeCommand>._, TestStringHelper.UserId)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task CreateInviteCode_Passes_The_Admins_App_To_The_Command()
  {
    // Arrange
    var command = new CreateInviteCodeCommand
    {
      InviteCode = "NEWINVITE",
      UserEmail = "owner@example.com",
      Role = "testapp:owner"
    };

    A.CallTo(() => _registeredAppResolver.GetActiveAppAsync(A<string>._, A<CancellationToken>._))
        .Returns(new RegisteredAppEntity { Id = 4, AppName = "TestApp" });
    A.CallTo(() => _createInviteCodeCommandHandler.CreateItemAsync(A<CreateInviteCodeCommand>._, TestStringHelper.UserId, A<CancellationToken>._))
        .Returns(new InviteCodeViewModel { InviteCode = new InviteCodeDto { Id = 1, InviteCode = command.InviteCode, UserEmail = command.UserEmail, Role = command.Role } });

    var controller = GetInviteCodeController();
    controller.UpdateContext(ControllerName, addUserId: true, userId: TestStringHelper.UserId, appName: "TestApp");

    // Act
    await controller.CreateInviteCode(command);

    // Assert
    A.CallTo(() => _createInviteCodeCommandHandler.CreateItemAsync(
        A<CreateInviteCodeCommand>.That.Matches(c => c.AppId == 4 && c.AppName == "TestApp" && c.Role == "testapp:owner"), TestStringHelper.UserId, A<CancellationToken>._))
        .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task GetInviteCodes_Returns_The_Apps_InviteCodes()
  {
    // Arrange
    var inviteCodesVM = new InviteCodesViewModel
    {
      InviteCodes = [new InviteCodeDto { Id = 1, InviteCode = "INVITE", UserEmail = "owner@example.com", AppId = 4 }],
      TotalCount = 1
    };

    A.CallTo(() => _registeredAppResolver.GetActiveAppAsync(A<string>._, A<CancellationToken>._))
        .Returns(new RegisteredAppEntity { Id = 4, AppName = "TestApp" });
    A.CallTo(() => _getInviteCodesQueryHandler.GetItemsAsync(A<GetInviteCodesQuery>.That.Matches(q => q.AppId == 4 && q.Page == 2 && q.PageSize == 5), A<CancellationToken>._))
        .Returns(inviteCodesVM);

    var controller = GetInviteCodeController();
    controller.UpdateContext(ControllerName, addUserId: true, userId: TestStringHelper.UserId, appName: "TestApp");

    // Act
    var actionResult = await controller.GetInviteCodes(2, 5);
    var result = actionResult?.Result as ObjectResult;
    var vm = result?.Value as InviteCodesViewModel;

    // Assert
    Assert.Equal((int)HttpStatusCode.OK, result?.StatusCode);
    Assert.NotNull(vm);
    Assert.Equal(1, vm.TotalCount);
    Assert.Single(vm.InviteCodes);
  }

  [Fact]
  public async Task DeleteInviteCode_Deletes_The_Apps_InviteCode()
  {
    // Arrange
    A.CallTo(() => _registeredAppResolver.GetActiveAppAsync(A<string>._, A<CancellationToken>._))
        .Returns(new RegisteredAppEntity { Id = 4, AppName = "TestApp" });
    A.CallTo(() => _deleteInviteCodeCommandHandler.DeleteItemAsync(A<DeleteInviteCodeCommand>._, TestStringHelper.UserId, A<CancellationToken>._))
        .Returns(new DeleteRecordViewModel());

    var controller = GetInviteCodeController();
    controller.UpdateContext(ControllerName, addUserId: true, userId: TestStringHelper.UserId, appName: "TestApp");

    // Act
    var actionResult = await controller.DeleteInviteCode(7);
    var result = actionResult?.Result as ObjectResult;

    // Assert
    Assert.Equal((int)HttpStatusCode.OK, result?.StatusCode);
    A.CallTo(() => _deleteInviteCodeCommandHandler.DeleteItemAsync(
        A<DeleteInviteCodeCommand>.That.Matches(c => c.InviteCodeId == 7 && c.AppId == 4), TestStringHelper.UserId, A<CancellationToken>._))
        .MustHaveHappenedOnceExactly();
  }

  private InviteCodeController GetInviteCodeController()
  {
    return new InviteCodeController(
        _createInviteCodeCommandHandler,
        _getInviteCodeQueryHandler,
        _getInviteCodesQueryHandler,
        _deleteInviteCodeCommandHandler,
        _registeredAppResolver);
  }
}
