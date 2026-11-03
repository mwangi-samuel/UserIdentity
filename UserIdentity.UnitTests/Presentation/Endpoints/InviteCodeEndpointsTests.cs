using System.Net;

using FakeItEasy;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Common.Enums;
using PolyzenKit.Domain.Entities.RegisteredApps;

using UserIdentity.Application.Core.InviteCodes.Commands;
using UserIdentity.Application.Core.InviteCodes.Queries;
using UserIdentity.Application.Core.InviteCodes.ViewModels;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Presentation.Endpoints;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Presentation.Endpoints;

public class InviteCodeEndpointsTests
{
  private readonly ICreateItemCommandHandler<CreateInviteCodeCommand, InviteCodeViewModel> _createInviteCodeCommandHandler;
  private readonly IGetItemQueryHandler<GetInviteCodeQuery, InviteCodeViewModel> _getInviteCodeQueryHandler;
  private readonly IGetItemsQueryHandler<GetInviteCodesQuery, InviteCodesViewModel> _getInviteCodesQueryHandler;
  private readonly IDeleteItemCommandHandler<DeleteInviteCodeCommand, DeleteRecordViewModel> _deleteInviteCodeCommandHandler;
  private readonly IRegisteredAppResolver _registeredAppResolver;

  public InviteCodeEndpointsTests()
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

    A.CallTo(() => _getInviteCodeQueryHandler.GetItemAsync(
      A<GetInviteCodeQuery>.That.Matches(q => q.InviteCodeId == inviteCodeId),
      A<CancellationToken>._))
        .Returns(inviteCodeVM);

    var request = ApiRequestFactory.Create("invite-code");

    // Act
    var result = await InviteCodeEndpoints.GetInviteCode(
      inviteCodeId,
      request,
      _registeredAppResolver,
      _getInviteCodeQueryHandler,
      TestContext.Current.CancellationToken);
    var vm = result.Value;

    // Assert
    Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
    Assert.NotNull(vm);
    Assert.Equal(inviteCodeVM.InviteCode.Id, vm?.InviteCode.Id);
    Assert.Equal(inviteCodeVM.InviteCode.InviteCode, vm?.InviteCode.InviteCode);
    Assert.Equal(inviteCodeVM.InviteCode.UserEmail, vm?.InviteCode.UserEmail);

    Assert.Contains(RequestStatus.Successful.ToText(), vm?.RequestStatus);
    Assert.Contains(ItemStatusMessage.FetchItemSuccessful.ToText(), vm?.StatusMessage);
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

    A.CallTo(() => _getInviteCodeQueryHandler.GetItemAsync(
      A<GetInviteCodeQuery>.That.Matches(q => q.UserEmail == userEmail),
      A<CancellationToken>._))
        .Returns(inviteCodeVM);

    var request = ApiRequestFactory.Create("invite-code");

    // Act
    var result = await InviteCodeEndpoints.GetInviteCodeByEmail(
      userEmail,
      request,
      _registeredAppResolver,
      _getInviteCodeQueryHandler,
      TestContext.Current.CancellationToken);
    var vm = result.Value;

    // Assert
    Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
    Assert.NotNull(vm);
    Assert.Equal(inviteCodeVM.InviteCode.UserEmail, vm?.InviteCode.UserEmail);
    Assert.Equal(inviteCodeVM.InviteCode.InviteCode, vm?.InviteCode.InviteCode);

    Assert.Contains(RequestStatus.Successful.ToText(), vm?.RequestStatus);
    Assert.Contains(ItemStatusMessage.FetchItemSuccessful.ToText(), vm?.StatusMessage);
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
    A.CallTo(() => _createInviteCodeCommandHandler.CreateItemAsync(
      A<CreateInviteCodeCommand>._,
      TestStringHelper.UserId,
      A<CancellationToken>._))
        .Returns(inviteCodeVM);

    var request = ApiRequestFactory.Create("invite-code", userId: TestStringHelper.UserId, appName: appName);

    // Act
    var result = await InviteCodeEndpoints.CreateInviteCode(
      command,
      request,
      _registeredAppResolver,
      _createInviteCodeCommandHandler,
      TestContext.Current.CancellationToken);
    var vm = result.Value;

    // Assert
    Assert.Equal((int)HttpStatusCode.Created, result.StatusCode);
    Assert.NotNull(vm);
    Assert.Equal(inviteCodeVM.InviteCode.InviteCode, vm?.InviteCode.InviteCode);
    Assert.Equal(inviteCodeVM.InviteCode.UserEmail, vm?.InviteCode.UserEmail);
    Assert.Equal(appId, vm?.InviteCode.AppId);

    Assert.Contains(RequestStatus.Successful.ToText(), vm?.RequestStatus);
    Assert.Contains(ItemStatusMessage.CreateItemSuccessful.ToText(), vm?.StatusMessage);

    A.CallTo(() => _registeredAppResolver.GetActiveAppAsync(A<string>._, A<CancellationToken>._))
      .MustHaveHappenedOnceExactly();
    A.CallTo(() => _createInviteCodeCommandHandler.CreateItemAsync(
      A<CreateInviteCodeCommand>._,
      TestStringHelper.UserId,
      A<CancellationToken>._))
      .MustHaveHappenedOnceExactly();
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
    A.CallTo(() => _createInviteCodeCommandHandler.CreateItemAsync(
      A<CreateInviteCodeCommand>._,
      TestStringHelper.UserId,
      A<CancellationToken>._))
        .Returns(new InviteCodeViewModel { InviteCode = new InviteCodeDto { Id = 1, InviteCode = command.InviteCode, UserEmail = command.UserEmail, Role = command.Role } });

    var request = ApiRequestFactory.Create("invite-code", userId: TestStringHelper.UserId, appName: "TestApp");

    // Act
    await InviteCodeEndpoints.CreateInviteCode(
      command,
      request,
      _registeredAppResolver,
      _createInviteCodeCommandHandler,
      TestContext.Current.CancellationToken);

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
    A.CallTo(() => _getInviteCodesQueryHandler.GetItemsAsync(
      A<GetInviteCodesQuery>.That.Matches(q => q.AppId == 4 && q.Page == 2 && q.PageSize == 5),
      A<CancellationToken>._))
        .Returns(inviteCodesVM);

    var request = ApiRequestFactory.Create("invite-code", userId: TestStringHelper.UserId, appName: "TestApp");

    // Act
    var result = await InviteCodeEndpoints.GetInviteCodes(
      request,
      _registeredAppResolver,
      _getInviteCodesQueryHandler,
      TestContext.Current.CancellationToken,
      2,
      5);
    var vm = result.Value;

    // Assert
    Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
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
    A.CallTo(() => _deleteInviteCodeCommandHandler.DeleteItemAsync(
      A<DeleteInviteCodeCommand>._,
      TestStringHelper.UserId,
      A<CancellationToken>._))
        .Returns(new DeleteRecordViewModel());

    var request = ApiRequestFactory.Create("invite-code", userId: TestStringHelper.UserId, appName: "TestApp");

    // Act
    var result = await InviteCodeEndpoints.DeleteInviteCode(
      7,
      request,
      _registeredAppResolver,
      _deleteInviteCodeCommandHandler,
      TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
    A.CallTo(() => _deleteInviteCodeCommandHandler.DeleteItemAsync(
        A<DeleteInviteCodeCommand>.That.Matches(c => c.InviteCodeId == 7 && c.AppId == 4), TestStringHelper.UserId, A<CancellationToken>._))
        .MustHaveHappenedOnceExactly();
  }
}
