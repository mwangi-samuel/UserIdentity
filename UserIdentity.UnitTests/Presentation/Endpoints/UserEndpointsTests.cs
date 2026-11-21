using System.Net;
using FakeItEasy;
using Microsoft.AspNetCore.Http;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Common.Enums;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Presentation.Endpoints;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.TwoFactor.Commands;
using UserIdentity.Application.Core.TwoFactor.Queries;
using UserIdentity.Application.Core.TwoFactor.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Queries;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Common.Enums;
using UserIdentity.Domain.Users;
using UserIdentity.Presentation.Endpoints;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Presentation.Endpoints;

public class UserEndpointsTests
{
    private readonly IRegisteredAppResolver _registeredAppResolver = A.Fake<IRegisteredAppResolver>();

    private readonly ActiveRegisteredApp _registeredApp = new()
    {
        Id = 1,
        AppName = "test-app",
        BaseUrl = "https://app.test",
    };

    private readonly AuthUserViewModel _session = new()
    {
        User = new UserDto { Id = "user-id" },
        NextStep = SignInStep.Session,
    };

    public UserEndpointsTests()
    {
        A.CallTo(() => _registeredAppResolver.GetActiveAppAsync(A<string>._, A<CancellationToken>._)).Returns(_registeredApp);
    }

    [Fact]
    public async Task Get_User_Passes_The_Callers_App_And_Grants_No_Rights_On_Another_Users_Record()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var handler = A.Fake<IGetItemQueryHandler<GetUserQuery, UserViewModel>>();

        A.CallTo(() => handler.GetItemAsync(A<GetUserQuery>._, A<CancellationToken>._))
            .Returns(
                new UserViewModel
                {
                    User = new UserDto { Id = userId, CreatedBy = userId },
                }
            );

        var request = ApiRequestFactory.Create("user", userId: TestStringHelper.UserId);

        // Act
        var result = await UserEndpoints.GetUserAsync(userId, request, handler, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.False(result.Value?.EditEnabled);
        Assert.Equal(ItemStatusMessage.FetchItemSuccessful.ToText(), result.Value?.StatusMessage);
        A.CallTo(() => handler.GetItemAsync(new GetUserQuery { UserId = userId, AppName = "testapp" }, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Get_User_By_Email_Passes_The_Admins_App_And_Who_Asks()
    {
        // Arrange
        var handler = A.Fake<IGetItemQueryHandler<GetUserByEmailQuery, UserViewModel>>();

        A.CallTo(() => handler.GetItemAsync(A<GetUserByEmailQuery>._, A<CancellationToken>._))
            .Returns(
                new UserViewModel
                {
                    User = new UserDto { Id = "user-id", Email = "user@test.com" },
                }
            );

        var request = ApiRequestFactory.Create("user", userId: TestStringHelper.UserId, roles: ["testapp:admin"]);

        // Act
        var result = await UserEndpoints.GetUserByEmailAsync(
            "user@test.com",
            request,
            _registeredAppResolver,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("user-id", result.Value?.User.Id);
        A.CallTo(() => _registeredAppResolver.GetActiveAppAsync("testapp", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() =>
                handler.GetItemAsync(
                    A<GetUserByEmailQuery>.That.Matches(q =>
                        q.Email == "user@test.com"
                        && q.RegisteredApp == _registeredApp
                        && q.Changer == new UserChanger(TestStringHelper.UserId, false)
                    ),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Create_User_Passes_The_Requests_App_And_Context_And_Returns_Created()
    {
        // Arrange
        var command = GetRegisterUserCommand();
        var handler = A.Fake<ICreateItemCommandHandler<RegisterUserCommand, AuthUserViewModel>>();

        A.CallTo(() => handler.CreateItemAsync(command, A<string>._, A<CancellationToken>._)).Returns(_session);

        var httpContext = GetClientHttpContext();

        // Act
        var result = await UserEndpoints.CreateUserAsync(
            command,
            ApiRequest.For(httpContext),
            httpContext,
            _registeredAppResolver,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.Created, result.StatusCode);
        Assert.Same(_registeredApp, command.RegisteredApp);
        Assert.Equal(RequestContext.Create("203.0.113.7", "Test Browser"), command.RequestContext);
        Assert.Equal(ItemStatusMessage.CreateItemSuccessful.ToText(), result.Value?.StatusMessage);
    }

    [Fact]
    public async Task Create_User_For_Service_Takes_The_App_From_The_Signature_And_Audits_As_The_System()
    {
        // Arrange
        var command = new RegisterServiceUserCommand
        {
            FirstName = "Test",
            UserName = "test.user",
            UserEmail = "test.user@test.com",
            UserPassword = "testPassword",
        };
        var handler = A.Fake<ICreateItemCommandHandler<RegisterServiceUserCommand, UserViewModel>>();

        A.CallTo(() => handler.CreateItemAsync(command, A<string>._, A<CancellationToken>._))
            .Returns(new UserViewModel { User = new UserDto { Id = "user-id" } });

        var request = ApiRequestFactory.Create("user", authorizedParty: "signedapp");

        // Act
        var result = await UserEndpoints.CreateUserForServiceAsync(
            command,
            request,
            _registeredAppResolver,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.Created, result.StatusCode);
        Assert.Same(_registeredApp, command.RegisteredApp);
        A.CallTo(() => _registeredAppResolver.GetActiveAppAsync("signedapp", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => handler.CreateItemAsync(command, KnownUsers.SystemId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Login_User_Passes_The_Requests_App_And_Context_And_Answers_Login_Successful()
    {
        // Arrange
        LoginUserCommand command = new() { UserName = "test.user", Password = "-test.user*1+" };
        var handler = A.Fake<ICreateItemCommandHandler<LoginUserCommand, AuthUserViewModel>>();

        A.CallTo(() => handler.CreateItemAsync(command, A<string>._, A<CancellationToken>._)).Returns(_session);

        var httpContext = GetClientHttpContext();

        // Act
        var result = await UserEndpoints.LoginUserAsync(
            command,
            ApiRequest.For(httpContext),
            httpContext,
            _registeredAppResolver,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Same(_registeredApp, command.RegisteredApp);
        Assert.Equal(RequestContext.Create("203.0.113.7", "Test Browser"), command.RequestContext);
        Assert.Equal("Login successful", result.Value?.StatusMessage);
    }

    [Fact]
    public async Task Refresh_Token_Grants_Edit_Only_And_Answers_Refresh_Token_Generated()
    {
        // Arrange
        ExchangeRefreshTokenCommand command = new() { AccessToken = "access-token", RefreshToken = "refresh-token" };
        var handler = A.Fake<IUpdateItemCommandHandler<ExchangeRefreshTokenCommand, ExchangeRefreshTokenViewModel>>();

        A.CallTo(() => handler.UpdateItemAsync(command, A<string>._, A<CancellationToken>._))
            .Returns(
                new ExchangeRefreshTokenViewModel
                {
                    UserToken = new UserTokenDto
                    {
                        AccessToken = new AccessTokenDto { Token = "new" },
                        RefreshToken = "new-refresh",
                    },
                }
            );

        var request = ApiRequestFactory.Create("user", userId: TestStringHelper.UserId);

        // Act
        var result = await UserEndpoints.ExchangeRefreshTokenAsync(command, request, handler, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.True(result.Value?.EditEnabled);
        Assert.False(result.Value?.DeleteEnabled);
        Assert.Equal("Refresh token generated successfully", result.Value?.StatusMessage);
    }

    [Fact]
    public async Task Confirm_Account_Passes_The_Requests_App_And_Context_And_Answers_Account_Confirmed()
    {
        // Arrange
        ConfirmAccountCommand command = new() { Ticket = "ticket", Code = "123456" };
        var handler = A.Fake<ICreateItemCommandHandler<ConfirmAccountCommand, AuthUserViewModel>>();

        A.CallTo(() => handler.CreateItemAsync(command, A<string>._, A<CancellationToken>._)).Returns(_session);

        var httpContext = GetClientHttpContext();

        // Act
        var result = await UserEndpoints.ConfirmAccountAsync(
            command,
            ApiRequest.For(httpContext),
            httpContext,
            _registeredAppResolver,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Same(_registeredApp, command.RegisteredApp);
        Assert.Equal(SignInStep.Session, result.Value?.NextStep);
        Assert.Equal("Account confirmed", result.Value?.StatusMessage);
    }

    [Fact]
    public async Task Resend_Code_Passes_The_Requests_App_And_Answers_Ok_With_No_Body()
    {
        // Arrange
        ResendCodeCommand command = new() { Ticket = "ticket" };
        var handler = A.Fake<ICreateItemCommandHandler<ResendCodeCommand, ResendCodeViewModel>>();

        var request = ApiRequestFactory.Create("user", appName: "test-app");

        // Act
        var result = await UserEndpoints.ResendCodeAsync(
            command,
            request,
            _registeredAppResolver,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Same(_registeredApp, command.RegisteredApp);
        A.CallTo(() => handler.CreateItemAsync(command, A<string>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Logout_User_Passes_The_Callers_Id_And_Answers_Logout_Successful()
    {
        // Arrange
        LogoutUserCommand command = new() { RefreshToken = "refresh" };
        var handler = A.Fake<IDeleteItemCommandHandler<LogoutUserCommand, DeleteRecordViewModel>>();

        A.CallTo(() => handler.DeleteItemAsync(command, TestStringHelper.UserId, A<CancellationToken>._))
            .Returns(new DeleteRecordViewModel());

        var request = ApiRequestFactory.Create("user", userId: TestStringHelper.UserId);

        // Act
        var result = await UserEndpoints.LogoutUserAsync(command, request, handler, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Logout successful", result.Value?.StatusMessage);
        A.CallTo(() => handler.DeleteItemAsync(command, TestStringHelper.UserId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Reset_Password_Passes_The_Requests_App_And_Context()
    {
        // Arrange
        ResetPasswordCommand command = new() { UserName = "user@server.com" };
        var handler = A.Fake<ICreateItemCommandHandler<ResetPasswordCommand, ResetPasswordViewModel>>();

        A.CallTo(() => handler.CreateItemAsync(command, A<string>._, A<CancellationToken>._))
            .Returns(
                new ResetPasswordViewModel
                {
                    ResetPasswordDetails = new ResetPasswordDto { EmailMessage = "message", ResetRequestId = "reset" },
                }
            );

        var httpContext = GetClientHttpContext();

        // Act
        var result = await UserEndpoints.ResetPasswordAsync(
            command,
            ApiRequest.For(httpContext),
            httpContext,
            _registeredAppResolver,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Same(_registeredApp, command.RegisteredApp);
        Assert.Equal(RequestContext.Create("203.0.113.7", "Test Browser"), command.RequestContext);
        Assert.Equal("Password reset request successful", result.Value?.StatusMessage);
    }

    [Fact]
    public async Task Update_Password_Passes_The_Requests_App_And_Context_And_Answers_With_The_Next_Step()
    {
        // Arrange
        UpdatePasswordCommand command = new()
        {
            ResetRequestId = "reset",
            Code = "123456",
            NewPassword = "NewPassword123",
        };
        var handler = A.Fake<IUpdateItemCommandHandler<UpdatePasswordCommand, AuthUserViewModel>>();

        A.CallTo(() => handler.UpdateItemAsync(command, A<string>._, A<CancellationToken>._)).Returns(_session);

        var httpContext = GetClientHttpContext();

        // Act
        var result = await UserEndpoints.UpdatePasswordAsync(
            command,
            ApiRequest.For(httpContext),
            httpContext,
            _registeredAppResolver,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal("Password updated successfully", result.Value?.StatusMessage);
        Assert.Equal(SignInStep.Session, result.Value?.NextStep);
        Assert.Equal(RequestContext.Create("203.0.113.7", "Test Browser"), command.RequestContext);
    }

    [Fact]
    public async Task Update_User_Passes_The_Target_The_Callers_App_And_Who_Changes()
    {
        // Arrange
        UpdateUserCommand command = new() { FirstName = "New" };
        var handler = A.Fake<IUpdateItemCommandHandler<UpdateUserCommand, UserViewModel>>();

        A.CallTo(() => handler.UpdateItemAsync(command, A<string>._, A<CancellationToken>._))
            .Returns(
                new UserViewModel
                {
                    User = new UserDto { Id = "target-id", CreatedBy = "target-id" },
                }
            );

        var httpContext = GetClientHttpContext(userId: TestStringHelper.UserId);

        // Act
        var result = await UserEndpoints.UpdateUserAsync(
            "target-id",
            command,
            ApiRequest.For(httpContext),
            httpContext,
            _registeredAppResolver,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("target-id", command.UserId);
        Assert.Same(_registeredApp, command.RegisteredApp);
        Assert.Equal(new UserChanger(TestStringHelper.UserId, false), command.Changer);
        Assert.Equal(RequestContext.Create("203.0.113.7", "Test Browser"), command.RequestContext);
        A.CallTo(() => _registeredAppResolver.GetActiveAppAsync("testapp", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Change_Email_Passes_The_Target_The_Callers_App_And_Who_Changes()
    {
        // Arrange
        ChangeEmailCommand command = new() { Email = "new@test.com", Password = "password" };
        var handler = A.Fake<ICreateItemCommandHandler<ChangeEmailCommand, EmailChangeViewModel>>();

        A.CallTo(() => handler.CreateItemAsync(command, A<string>._, A<CancellationToken>._))
            .Returns(
                new EmailChangeViewModel
                {
                    User = new UserDto { Id = TestStringHelper.UserId },
                    CodeSentTo = "n***@test.com",
                }
            );

        var httpContext = GetClientHttpContext(userId: TestStringHelper.UserId);

        // Act
        var result = await UserEndpoints.ChangeEmailAsync(
            TestStringHelper.UserId,
            command,
            ApiRequest.For(httpContext),
            httpContext,
            _registeredAppResolver,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal("n***@test.com", result.Value?.CodeSentTo);
        Assert.Equal(TestStringHelper.UserId, command.UserId);
        Assert.Equal(new UserChanger(TestStringHelper.UserId, false), command.Changer);
    }

    [Fact]
    public async Task Confirm_Email_Change_Passes_The_Target_The_Callers_App_And_Who_Changes()
    {
        // Arrange
        ConfirmEmailChangeCommand command = new() { Code = "123456" };
        var handler = A.Fake<ICreateItemCommandHandler<ConfirmEmailChangeCommand, UserViewModel>>();

        A.CallTo(() => handler.CreateItemAsync(command, A<string>._, A<CancellationToken>._))
            .Returns(new UserViewModel { User = new UserDto { Id = TestStringHelper.UserId } });

        var httpContext = GetClientHttpContext(userId: TestStringHelper.UserId);

        // Act
        await UserEndpoints.ConfirmEmailChangeAsync(
            TestStringHelper.UserId,
            command,
            ApiRequest.For(httpContext),
            httpContext,
            _registeredAppResolver,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(TestStringHelper.UserId, command.UserId);
        Assert.Same(_registeredApp, command.RegisteredApp);
        Assert.Equal(new UserChanger(TestStringHelper.UserId, false), command.Changer);
    }

    [Fact]
    public async Task Two_Factor_Status_Is_Read_For_The_Callers_Own_Account_In_The_Tokens_App()
    {
        // Arrange
        var handler = A.Fake<IGetItemQueryHandler<GetTwoFactorStatusQuery, TwoFactorStatusViewModel>>();

        A.CallTo(() => handler.GetItemAsync(A<GetTwoFactorStatusQuery>._, A<CancellationToken>._))
            .Returns(new TwoFactorStatusViewModel { AllowedMethods = [TwoFactorMethod.Authenticator] });

        var request = ApiRequestFactory.Create("user", userId: TestStringHelper.UserId);

        // Act
        var result = await UserEndpoints.GetTwoFactorStatusAsync(
            request,
            _registeredAppResolver,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([TwoFactorMethod.Authenticator], result.Value?.AllowedMethods);
        A.CallTo(() => _registeredAppResolver.GetActiveAppAsync("testapp", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => handler.GetItemAsync(new GetTwoFactorStatusQuery(TestStringHelper.UserId, _registeredApp), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Verify_Second_Factor_Passes_The_Requests_App_And_Context()
    {
        // Arrange
        VerifySecondFactorCommand command = new() { Ticket = "ticket", Code = "123456" };
        var handler = A.Fake<ICreateItemCommandHandler<VerifySecondFactorCommand, AuthUserViewModel>>();

        A.CallTo(() => handler.CreateItemAsync(command, A<string>._, A<CancellationToken>._)).Returns(_session);

        var httpContext = GetClientHttpContext();

        // Act
        var result = await UserEndpoints.VerifySecondFactorAsync(
            command,
            ApiRequest.For(httpContext),
            httpContext,
            _registeredAppResolver,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(SignInStep.Session, result.Value?.NextStep);
        Assert.Same(_registeredApp, command.RegisteredApp);
        Assert.Equal(RequestContext.Create("203.0.113.7", "Test Browser"), command.RequestContext);
    }

    [Fact]
    public async Task Disable_Two_Factor_Acts_On_The_Callers_Own_Account()
    {
        // Arrange
        DisableTwoFactorCommand command = new() { Password = "password" };
        var handler = A.Fake<IUpdateItemCommandHandler<DisableTwoFactorCommand, TwoFactorStatusViewModel>>();

        A.CallTo(() => handler.UpdateItemAsync(command, A<string>._, A<CancellationToken>._))
            .Returns(new TwoFactorStatusViewModel { AllowedMethods = [] });

        var httpContext = GetClientHttpContext(userId: TestStringHelper.UserId);

        // Act
        await UserEndpoints.DisableTwoFactorAsync(
            command,
            ApiRequest.For(httpContext),
            httpContext,
            _registeredAppResolver,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(TestStringHelper.UserId, command.UserId);
        Assert.Same(_registeredApp, command.RegisteredApp);
    }

    [Fact]
    public async Task Reset_User_Two_Factor_Passes_The_Admins_App_And_Who_Resets()
    {
        // Arrange
        ResetUserTwoFactorCommand command = new() { Email = "user@test.com" };
        var handler = A.Fake<IUpdateItemCommandHandler<ResetUserTwoFactorCommand, UserViewModel>>();

        A.CallTo(() => handler.UpdateItemAsync(command, A<string>._, A<CancellationToken>._))
            .Returns(new UserViewModel { User = new UserDto { Id = "user-id" } });

        var request = ApiRequestFactory.Create("user", userId: TestStringHelper.UserId, roles: ["testapp:admin"]);

        // Act
        await UserEndpoints.ResetUserTwoFactorAsync(
            command,
            request,
            _registeredAppResolver,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Same(_registeredApp, command.RegisteredApp);
        Assert.Equal(new UserChanger(TestStringHelper.UserId, false), command.Changer);
    }

    [Fact]
    public void Request_Context_Reads_An_Ipv4_Mapped_Address_As_Ipv4()
    {
        // Arrange
        var httpContext = GetClientHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7").MapToIPv6();

        // Act
        var requestContext = RequestContextReader.Read(httpContext);

        // Assert
        Assert.Equal("203.0.113.7", requestContext.IpAddress);
        Assert.Equal("Test Browser", requestContext.Device);
    }

    private static HttpContext GetClientHttpContext(string? userId = null)
    {
        var httpContext = ApiRequestFactory.CreateHttpContext("user", userId: userId, appName: "test-app");

        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
        httpContext.Request.Headers.UserAgent = "Test Browser";

        return httpContext;
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
