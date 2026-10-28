using System;
using System.Net;
using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.AspNetCore.Mvc;

using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Resolvers;
using PolyzenKit.Application.Enums;

using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Queries;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Domain.Users;
using UserIdentity.Presentation.Controllers;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Presentation.Controllers;

public class UserControllerTests
{
  private readonly static string Controllername = "user";

  private readonly IGetItemQueryHandler<GetUserQuery, UserViewModel> _getUserQueryHandler;
  private readonly ICreateItemCommandHandler<RegisterUserCommand, AuthUserViewModel> _registerUserCommandHandler;
  private readonly ICreateItemCommandHandler<LoginUserCommand, AuthUserViewModel> _loginUserCommandHandler;
  private readonly IUpdateItemCommandHandler<ExchangeRefreshTokenCommand, ExchangeRefreshTokenViewModel> _exchangeRefreshTokenCommandHandler;
  private readonly IDeleteItemCommandHandler<LogoutUserCommand, DeleteRecordViewModel> _logoutUserCommandHandler;
  private readonly ICreateItemCommandHandler<ResetPasswordCommand, ResetPasswordViewModel> _resetPasswordCommandHandler;
  private readonly IUpdateItemCommandHandler<UpdatePasswordCommand, UpdatePasswordViewModel> _updatePasswordCommandHandler;

  private readonly IRegisteredAppResolver _registeredAppResolver;

  public UserControllerTests()
  {
    _getUserQueryHandler = A.Fake<IGetItemQueryHandler<GetUserQuery, UserViewModel>>();
    _registerUserCommandHandler = A.Fake<ICreateItemCommandHandler<RegisterUserCommand, AuthUserViewModel>>();
    _loginUserCommandHandler = A.Fake<ICreateItemCommandHandler<LoginUserCommand, AuthUserViewModel>>();
    _exchangeRefreshTokenCommandHandler = A.Fake<IUpdateItemCommandHandler<ExchangeRefreshTokenCommand, ExchangeRefreshTokenViewModel>>();
    _logoutUserCommandHandler = A.Fake<IDeleteItemCommandHandler<LogoutUserCommand, DeleteRecordViewModel>>();
    _resetPasswordCommandHandler = A.Fake<ICreateItemCommandHandler<ResetPasswordCommand, ResetPasswordViewModel>>();
    _updatePasswordCommandHandler = A.Fake<IUpdateItemCommandHandler<UpdatePasswordCommand, UpdatePasswordViewModel>>();
    _registeredAppResolver = A.Fake<IRegisteredAppResolver>();
  }

  [Fact]
  public async Task Get_User_Should_Return_User()
  {
    // Arrange
    var userId = Guid.NewGuid().ToString();
    var userFName = "Test";
    var userLName = "User";
    var userName = "test.user";
    var userEmail = userName + "@test.com";
    GetUserQuery getUserQuery = new() { UserId = userId, AppName = "testapp" };
    UserViewModel userVM = new() { User = new UserDto { Id = userId, FirstName = userFName, LastName = userLName, UserName = userName, Email = userEmail } };

    // Act
    A.CallTo(() => _getUserQueryHandler.GetItemAsync(getUserQuery)).Returns(userVM);

    var controller = GetUserController();
    controller.UpdateContext(null);
    var actionResult = await controller.GetUser(userId);
    var result = actionResult?.Result as ObjectResult;
    var vm = result?.Value as UserViewModel;

    // Assert
    Assert.Equal((int)HttpStatusCode.OK, result?.StatusCode);

    Assert.NotNull(vm);
    Assert.Equal(userId, vm?.User?.Id);
    Assert.Equal(userFName, vm?.User?.FirstName);
    Assert.Equal(userLName, vm?.User?.LastName);
    Assert.Equal(userName, vm?.User?.UserName);
    Assert.Equal(userEmail, vm?.User?.Email);

    Assert.False(vm?.EditEnabled);
    Assert.False(vm?.DeleteEnabled);

    Assert.Contains(RequestStatus.SUCCESSFUL.Description(), vm?.RequestStatus);
    Assert.Contains(ItemStatusMessage.FETCH_ITEM_SUCCESSFUL.Description(), vm?.StatusMessage);
  }

  [Fact]
  public async Task Create_User_Should_Create_User()
  {
    // Arrange
    var userId = Guid.NewGuid().ToString();
    var userFName = "Test";
    var userLName = "User";
    var userName = "test.user";
    var userEmail = userName + "@test.com";
    var userPassword = "testPassword";

    var refreshToken = "abmsmmsrefreshToken";

    RegisterUserCommand command = new() { FirstName = userFName, LastName = userLName, UserName = userName, UserEmail = userEmail, UserPassword = userPassword };

    AuthUserViewModel authVM = new() { User = new UserDto { Id = userId, FirstName = userFName, LastName = userLName, UserName = userName, Email = userEmail }, UserToken = new AccessTokenViewModel { RefreshToken = refreshToken } };

    // Act
    A.CallTo(() => _registerUserCommandHandler.CreateItemAsync(command, TestStringHelper.UserId)).Returns(authVM);

    var controller = GetUserController();
    controller.UpdateContext(Controllername, addUserId: true, userId: TestStringHelper.UserId, appName: "test-app");
    var actionResult = await controller.CreateUser(command);
    var result = actionResult?.Result as ObjectResult;
    var vm = result?.Value as AuthUserViewModel;

    // Assert
    Assert.Equal((int)HttpStatusCode.Created, result?.StatusCode);

    Assert.NotNull(vm);
    Assert.Equal(userId, vm?.User?.Id);
    Assert.Equal(userFName, vm?.User?.FirstName);
    Assert.Equal(userLName, vm?.User?.LastName);
    Assert.Equal(userName, vm?.User?.UserName);
    Assert.Equal(userEmail, vm?.User?.Email);

    Assert.Equal(refreshToken, vm?.UserToken?.RefreshToken);

    Assert.True(vm?.EditEnabled);
    Assert.True(vm?.DeleteEnabled);

    Assert.Contains(RequestStatus.SUCCESSFUL.Description(), vm?.RequestStatus);
    Assert.Contains(ItemStatusMessage.CREATE_ITEM_SUCCESSFUL.Description(), vm?.StatusMessage);
  }

  [Fact]
  public async Task Login_User_Should_Login_User()
  {
    // Arrange
    var userName = "test.user";
    var userPassword = "-test.user*1+";

    var userId = Guid.NewGuid().ToString();
    var userFName = "Test";
    var userLName = "User";
    var userEmail = userName + "@test.com";

    var refreshToken = "abmsmmsrefreshToken";
    var loginMessage = "Login successful";

    LoginUserCommand command = new() { UserName = userName, Password = userPassword };
    AuthUserViewModel authVM = new() { User = new UserDto { Id = userId, FirstName = userFName, LastName = userLName, UserName = userName, Email = userEmail }, UserToken = new AccessTokenViewModel { RefreshToken = refreshToken } };

    // Act
    A.CallTo(() => _loginUserCommandHandler.CreateItemAsync(command, TestStringHelper.UserId)).Returns(authVM);

    var controller = GetUserController();
    controller.UpdateContext(Controllername, addUserId: true, userId: TestStringHelper.UserId, appName: "test-app");
    var actionResult = await controller.LoginUser(command);
    var result = actionResult?.Result as ObjectResult;
    var vm = result?.Value as AuthUserViewModel;

    // Assert
    Assert.Equal((int)HttpStatusCode.OK, result?.StatusCode);

    Assert.NotNull(vm);
    Assert.Equal(userId, vm?.User?.Id);
    Assert.Equal(userFName, vm?.User?.FirstName);
    Assert.Equal(userLName, vm?.User?.LastName);
    Assert.Equal(userName, vm?.User?.UserName);
    Assert.Equal(userEmail, vm?.User?.Email);
    Assert.Equal(refreshToken, vm?.UserToken?.RefreshToken);

    Assert.True(vm?.EditEnabled);
    Assert.True(vm?.DeleteEnabled);

    Assert.Contains(RequestStatus.SUCCESSFUL.Description(), vm?.RequestStatus);
    Assert.Contains(loginMessage, vm?.StatusMessage);
  }

  [Fact]
  public async Task Refresh_Token_Refreshes_Token()
  {
    // Arrange
    var accessToken = "Acccess Token This";
    var refreshToken = "Refresh Token This";

    var newAccesstoken = "New Access Token This";
    var newRefreshToken = "New Refresh Token This";

    var sucessStatusMessage = "Refresh token generated successfully";

    ExchangeRefreshTokenViewModel exchangeRefreshTokenVM = new() { UserToken = new AccessTokenViewModel { AccessToken = new AccessTokenDto { Token = newAccesstoken }, RefreshToken = newRefreshToken } };

    ExchangeRefreshTokenCommand command = new() { AccessToken = accessToken, RefreshToken = refreshToken };

    // Act
    A.CallTo(() => _exchangeRefreshTokenCommandHandler.UpdateItemAsync(command, TestStringHelper.UserId)).Returns(exchangeRefreshTokenVM);

    var controller = GetUserController();
    controller.UpdateContext(Controllername, addUserId: true, userId: TestStringHelper.UserId);
    var actionResult = await controller.RefreshToken(command);
    var result = actionResult?.Result as ObjectResult;
    var vm = result?.Value as ExchangeRefreshTokenViewModel;

    // Assert
    Assert.Equal((int)HttpStatusCode.OK, result?.StatusCode);

    Assert.NotNull(vm);
    Assert.Equal(newAccesstoken, vm?.UserToken?.AccessToken?.Token);
    Assert.Equal(newRefreshToken, vm?.UserToken?.RefreshToken);

    Assert.True(vm?.EditEnabled);
    Assert.False(vm?.DeleteEnabled);

    Assert.Contains(RequestStatus.SUCCESSFUL.Description(), vm?.RequestStatus);
    Assert.Contains(sucessStatusMessage, vm?.StatusMessage);
  }

  [Fact]
  public async Task Logout_User_Logs_Out_User()
  {
    // Arrange
    LogoutUserCommand command = new() { RefreshToken = "Refresh Token This" };

    // Act
    A.CallTo(() => _logoutUserCommandHandler.DeleteItemAsync(command, TestStringHelper.UserId)).Returns(new DeleteRecordViewModel());

    var controller = GetUserController();
    controller.UpdateContext(Controllername, addUserId: true, userId: TestStringHelper.UserId);
    var actionResult = await controller.LogoutUser(command);
    var result = actionResult?.Result as ObjectResult;
    var vm = result?.Value as DeleteRecordViewModel;

    // Assert
    Assert.Equal((int)HttpStatusCode.OK, result?.StatusCode);

    Assert.NotNull(vm);
    Assert.Contains(RequestStatus.SUCCESSFUL.Description(), vm?.RequestStatus);
    Assert.Contains("Logout successful", vm?.StatusMessage);
    A.CallTo(() => _logoutUserCommandHandler.DeleteItemAsync(command, TestStringHelper.UserId)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Reset_Password_Resets_Password()
  {
    // Arrange
    var userName = "user-email123@server.com";
    var resetRequestId = "ResetRequestId123";

    var emailMessage = "Password reset code sent successfully";
    var resetMessage = "Password reset request successful";

    ResetPasswordCommand command = new() { UserName = userName };
    ResetPasswordViewModel resetPasswordVM = new() { ResetPasswordDetails = new ResetPasswordDto { EmailMessage = emailMessage, ResetRequestId = resetRequestId } };

    // Act
    A.CallTo(() => _resetPasswordCommandHandler.CreateItemAsync(command, TestStringHelper.UserId)).Returns(resetPasswordVM);

    var controller = GetUserController();
    controller.UpdateContext(Controllername, addUserId: true, userId: TestStringHelper.UserId, appName: "test-app");
    var actionResult = await controller.ResetPassword(command);
    var result = actionResult?.Result as ObjectResult;
    var vm = result?.Value as ResetPasswordViewModel;

    // Assert
    Assert.Equal((int)HttpStatusCode.OK, result?.StatusCode);

    Assert.NotNull(vm);
    Assert.Equal(emailMessage, vm?.ResetPasswordDetails?.EmailMessage);
    Assert.Equal(resetRequestId, vm?.ResetPasswordDetails?.ResetRequestId);
    Assert.NotNull(command.RegisteredApp);

    Assert.Contains(RequestStatus.SUCCESSFUL.Description(), vm?.RequestStatus);
    Assert.Contains(resetMessage, vm?.StatusMessage);
  }

  [Fact]
  public async Task Update_Password_Updates_Password()
  {

    // Arrange
    var newPassword = "NewPassword123";
    var resetRequestId = "ResetRequestId123";

    UpdatePasswordCommand command = new() { ResetRequestId = resetRequestId, NewPassword = newPassword };
    UpdatePasswordViewModel updatePasswordViewModel = new() { UpdatePasswordResult = new UpdatePasswordDto { PassWordUpdated = true } };

    // Act
    A.CallTo(() => _updatePasswordCommandHandler.UpdateItemAsync(command, TestStringHelper.UserId)).Returns(updatePasswordViewModel);

    var controller = GetUserController();
    controller.UpdateContext(Controllername, addUserId: true, userId: TestStringHelper.UserId);
    var actionResult = await controller.UpdatePassword(command);
    var result = actionResult?.Result as ObjectResult;
    var vm = result?.Value as UpdatePasswordViewModel;

    // Assert
    Assert.Equal((int)HttpStatusCode.OK, result?.StatusCode);

    Assert.NotNull(vm);
    Assert.True(vm?.UpdatePasswordResult?.PassWordUpdated);

    Assert.Contains(RequestStatus.SUCCESSFUL.Description(), vm?.RequestStatus);
    Assert.Contains("Password updated successfully", vm?.StatusMessage);
  }

  private UserController GetUserController()
  {
    return new UserController(
            _getUserQueryHandler,
            _registerUserCommandHandler,
            _loginUserCommandHandler,
            _exchangeRefreshTokenCommandHandler,
            _logoutUserCommandHandler,
            _resetPasswordCommandHandler,
            _updatePasswordCommandHandler,
            _registeredAppResolver
    );

  }
}
