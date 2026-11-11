using System.Net;
using FakeItEasy;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Common.Enums;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Queries;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Common.Enums;
using UserIdentity.Domain.Users;
using UserIdentity.Presentation.Endpoints;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Presentation.Endpoints;

public class UserEndpointsTests
{
    private readonly IGetItemQueryHandler<GetUserQuery, UserViewModel> _getUserQueryHandler;
    private readonly ICreateItemCommandHandler<RegisterUserCommand, AuthUserViewModel> _registerUserCommandHandler;
    private readonly ICreateItemCommandHandler<LoginUserCommand, AuthUserViewModel> _loginUserCommandHandler;
    private readonly IUpdateItemCommandHandler<
        ExchangeRefreshTokenCommand,
        ExchangeRefreshTokenViewModel
    > _exchangeRefreshTokenCommandHandler;
    private readonly IDeleteItemCommandHandler<LogoutUserCommand, DeleteRecordViewModel> _logoutUserCommandHandler;
    private readonly ICreateItemCommandHandler<ResetPasswordCommand, ResetPasswordViewModel> _resetPasswordCommandHandler;
    private readonly IUpdateItemCommandHandler<UpdatePasswordCommand, UpdatePasswordViewModel> _updatePasswordCommandHandler;

    private readonly IRegisteredAppResolver _registeredAppResolver;

    private readonly ActiveRegisteredApp _registeredApp = new()
    {
        Id = 1,
        AppName = "test-app",
        AppSecretKey = "test-secret",
        BaseUrl = "https://app.test",
    };

    public UserEndpointsTests()
    {
        _getUserQueryHandler = A.Fake<IGetItemQueryHandler<GetUserQuery, UserViewModel>>();
        _registerUserCommandHandler = A.Fake<ICreateItemCommandHandler<RegisterUserCommand, AuthUserViewModel>>();
        _loginUserCommandHandler = A.Fake<ICreateItemCommandHandler<LoginUserCommand, AuthUserViewModel>>();
        _exchangeRefreshTokenCommandHandler = A.Fake<
            IUpdateItemCommandHandler<ExchangeRefreshTokenCommand, ExchangeRefreshTokenViewModel>
        >();
        _logoutUserCommandHandler = A.Fake<IDeleteItemCommandHandler<LogoutUserCommand, DeleteRecordViewModel>>();
        _resetPasswordCommandHandler = A.Fake<ICreateItemCommandHandler<ResetPasswordCommand, ResetPasswordViewModel>>();
        _updatePasswordCommandHandler = A.Fake<IUpdateItemCommandHandler<UpdatePasswordCommand, UpdatePasswordViewModel>>();
        _registeredAppResolver = A.Fake<IRegisteredAppResolver>();

        A.CallTo(() => _registeredAppResolver.GetActiveAppAsync(A<string>._, A<CancellationToken>._)).Returns(_registeredApp);
    }

    [Fact]
    public async Task Get_User_Passes_The_Callers_App_And_Grants_No_Rights_On_Another_Users_Record()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();

        A.CallTo(() => _getUserQueryHandler.GetItemAsync(A<GetUserQuery>._, A<CancellationToken>._))
            .Returns(
                new UserViewModel
                {
                    User = new UserDto { Id = userId, CreatedBy = userId },
                }
            );

        var request = ApiRequestFactory.Create("user", userId: TestStringHelper.UserId);

        // Act
        var result = await UserEndpoints.GetUserAsync(userId, request, _getUserQueryHandler, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.False(result.Value?.EditEnabled);
        Assert.False(result.Value?.DeleteEnabled);
        Assert.Equal(RequestStatus.Successful.ToText(), result.Value?.RequestStatus);
        Assert.Equal(ItemStatusMessage.FetchItemSuccessful.ToText(), result.Value?.StatusMessage);

        A.CallTo(() => _getUserQueryHandler.GetItemAsync(new GetUserQuery { UserId = userId, AppName = "testapp" }, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Get_User_Grants_Rights_On_The_Callers_Own_Record()
    {
        // Arrange
        A.CallTo(() => _getUserQueryHandler.GetItemAsync(A<GetUserQuery>._, A<CancellationToken>._))
            .Returns(
                new UserViewModel
                {
                    User = new UserDto { Id = TestStringHelper.UserId, CreatedBy = TestStringHelper.UserId },
                }
            );

        var request = ApiRequestFactory.Create("user", userId: TestStringHelper.UserId);

        // Act
        var result = await UserEndpoints.GetUserAsync(
            TestStringHelper.UserId,
            request,
            _getUserQueryHandler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(result.Value?.EditEnabled);
        Assert.True(result.Value?.DeleteEnabled);
    }

    [Fact]
    public async Task Create_User_Passes_The_Requests_App_And_Returns_Created()
    {
        // Arrange
        var command = GetRegisterUserCommand();

        A.CallTo(() => _registerUserCommandHandler.CreateItemAsync(command, A<string>._, A<CancellationToken>._))
            .Returns(new AuthUserViewModel { User = new UserDto { Id = "user-id" } });

        var request = ApiRequestFactory.Create("user", appName: "test-app");

        // Act
        var result = await UserEndpoints.CreateUserAsync(
            command,
            request,
            _registeredAppResolver,
            _registerUserCommandHandler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.Created, result.StatusCode);
        Assert.Same(_registeredApp, command.RegisteredApp);
        Assert.Equal(RequestSource.UI, command.RequestSource);
        Assert.True(result.Value?.EditEnabled);
        Assert.True(result.Value?.DeleteEnabled);
        Assert.Equal(RequestStatus.Successful.ToText(), result.Value?.RequestStatus);
        Assert.Equal(ItemStatusMessage.CreateItemSuccessful.ToText(), result.Value?.StatusMessage);
    }

    [Fact]
    public async Task Create_User_For_Service_Registers_The_User_As_An_Api_Sign_Up()
    {
        // Arrange
        var command = GetRegisterUserCommand();

        A.CallTo(() => _registerUserCommandHandler.CreateItemAsync(command, A<string>._, A<CancellationToken>._))
            .Returns(new AuthUserViewModel { User = new UserDto { Id = "user-id" } });

        var request = ApiRequestFactory.Create("user", userId: TestStringHelper.UserId, appName: "test-app");

        // Act
        var result = await UserEndpoints.CreateUserForServiceAsync(
            command,
            request,
            _registeredAppResolver,
            _registerUserCommandHandler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.Created, result.StatusCode);
        Assert.Equal(RequestSource.Api, command.RequestSource);
        Assert.Same(_registeredApp, command.RegisteredApp);
    }

    [Fact]
    public async Task Login_User_Passes_The_Requests_App_And_Answers_Login_Successful()
    {
        // Arrange
        LoginUserCommand command = new() { UserName = "test.user", Password = "-test.user*1+" };

        A.CallTo(() => _loginUserCommandHandler.CreateItemAsync(command, A<string>._, A<CancellationToken>._))
            .Returns(new AuthUserViewModel { User = new UserDto { Id = "user-id" } });

        var request = ApiRequestFactory.Create("user", appName: "test-app");

        // Act
        var result = await UserEndpoints.LoginUserAsync(
            command,
            request,
            _registeredAppResolver,
            _loginUserCommandHandler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Same(_registeredApp, command.RegisteredApp);
        Assert.True(result.Value?.EditEnabled);
        Assert.True(result.Value?.DeleteEnabled);
        Assert.Equal(RequestStatus.Successful.ToText(), result.Value?.RequestStatus);
        Assert.Equal("Login successful", result.Value?.StatusMessage);
    }

    [Fact]
    public async Task Refresh_Token_Grants_Edit_Only_And_Answers_Refresh_Token_Generated()
    {
        // Arrange
        ExchangeRefreshTokenCommand command = new() { AccessToken = "access-token", RefreshToken = "refresh-token" };

        A.CallTo(() => _exchangeRefreshTokenCommandHandler.UpdateItemAsync(command, A<string>._, A<CancellationToken>._))
            .Returns(
                new ExchangeRefreshTokenViewModel
                {
                    UserToken = new UserTokenDto
                    {
                        AccessToken = new AccessTokenDto { Token = "new-access-token" },
                        RefreshToken = "new-refresh-token",
                    },
                }
            );

        var request = ApiRequestFactory.Create("user", userId: TestStringHelper.UserId);

        // Act
        var result = await UserEndpoints.ExchangeRefreshTokenAsync(
            command,
            request,
            _exchangeRefreshTokenCommandHandler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.True(result.Value?.EditEnabled);
        Assert.False(result.Value?.DeleteEnabled);
        Assert.Equal(RequestStatus.Successful.ToText(), result.Value?.RequestStatus);
        Assert.Equal("Refresh token generated successfully", result.Value?.StatusMessage);
    }

    [Fact]
    public async Task Exchange_Login_Ticket_Starts_A_Session_For_The_Requests_App()
    {
        // Arrange
        ExchangeLoginTicketCommand command = new() { Ticket = "login-ticket" };
        var handler = A.Fake<ICreateItemCommandHandler<ExchangeLoginTicketCommand, AuthUserViewModel>>();

        A.CallTo(() => handler.CreateItemAsync(command, A<string>._, A<CancellationToken>._))
            .Returns(
                new AuthUserViewModel
                {
                    User = new UserDto { Id = "user-id" },
                    IsConfirmed = true,
                }
            );

        var request = ApiRequestFactory.Create("user", appName: "test-app");

        // Act
        var result = await UserEndpoints.ExchangeLoginTicketAsync(
            command,
            request,
            _registeredAppResolver,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Same(_registeredApp, command.RegisteredApp);
        Assert.True(result.Value?.EditEnabled);
        Assert.Equal("Login successful", result.Value?.StatusMessage);
    }

    [Fact]
    public async Task Logout_User_Passes_The_Callers_Id_And_Answers_Logout_Successful()
    {
        // Arrange
        LogoutUserCommand command = new() { RefreshToken = "Refresh Token This" };

        A.CallTo(() => _logoutUserCommandHandler.DeleteItemAsync(command, TestStringHelper.UserId, A<CancellationToken>._))
            .Returns(new DeleteRecordViewModel());

        var request = ApiRequestFactory.Create("user", userId: TestStringHelper.UserId);

        // Act
        var result = await UserEndpoints.LogoutUserAsync(
            command,
            request,
            _logoutUserCommandHandler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(RequestStatus.Successful.ToText(), result.Value?.RequestStatus);
        Assert.Equal("Logout successful", result.Value?.StatusMessage);
        A.CallTo(() => _logoutUserCommandHandler.DeleteItemAsync(command, TestStringHelper.UserId, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Reset_Password_Passes_The_Requests_App_And_Answers_Reset_Request_Successful()
    {
        // Arrange
        ResetPasswordCommand command = new() { UserName = "user-email123@server.com" };

        A.CallTo(() => _resetPasswordCommandHandler.CreateItemAsync(command, A<string>._, A<CancellationToken>._))
            .Returns(
                new ResetPasswordViewModel
                {
                    ResetPasswordDetails = new ResetPasswordDto { EmailMessage = "message", ResetRequestId = "ResetRequestId123" },
                }
            );

        var request = ApiRequestFactory.Create("user", appName: "test-app");

        // Act
        var result = await UserEndpoints.ResetPasswordAsync(
            command,
            request,
            _registeredAppResolver,
            _resetPasswordCommandHandler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Same(_registeredApp, command.RegisteredApp);
        Assert.Equal(RequestStatus.Successful.ToText(), result.Value?.RequestStatus);
        Assert.Equal("Password reset request successful", result.Value?.StatusMessage);
    }

    [Theory]
    [InlineData(true, HttpStatusCode.OK, "Password updated successfully")]
    [InlineData(false, HttpStatusCode.NotAcceptable, "Password update failed")]
    public async Task Update_Password_Answers_By_Whether_The_Password_Was_Updated(
        bool passwordUpdated,
        HttpStatusCode statusCode,
        string statusMessage
    )
    {
        // Arrange
        UpdatePasswordCommand command = new() { ResetRequestId = "ResetRequestId123", NewPassword = "NewPassword123" };

        A.CallTo(() => _updatePasswordCommandHandler.UpdateItemAsync(command, A<string>._, A<CancellationToken>._))
            .Returns(new UpdatePasswordViewModel { UpdatePasswordResult = new UpdatePasswordDto { PasswordUpdated = passwordUpdated } });

        var request = ApiRequestFactory.Create("user");

        // Act
        var result = await UserEndpoints.UpdatePasswordAsync(
            command,
            request,
            _updatePasswordCommandHandler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)statusCode, result.StatusCode);
        Assert.Equal(statusMessage, result.Value?.StatusMessage);
    }

    private static RegisterUserCommand GetRegisterUserCommand() =>
        new()
        {
            FirstName = "Test",
            UserName = "test.user",
            UserEmail = "test.user@test.com",
            UserPassword = "testPassword",
        };
}
