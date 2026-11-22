using System.Net;
using FakeItEasy;
using Microsoft.AspNetCore.Http;
using PolyzenKit.Application.Core;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Queries;
using PolyzenKit.Application.Core.RegisteredApps.ViewModels;
using PolyzenKit.Common.Enums;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Presentation.Endpoints;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Tokens.Queries;
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
    private readonly IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel> _getRegisteredAppHandler = A.Fake<
        IGetItemQueryHandler<GetRegisteredAppQuery, RegisteredAppViewModel>
    >();

    private readonly RegisteredAppDto _registeredApp = new()
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
        A.CallTo(() => _getRegisteredAppHandler.GetItemAsync(A<GetRegisteredAppQuery>._, A<CancellationToken>._))
            .Returns(new RegisteredAppViewModel { RegisteredApp = _registeredApp });
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
            _getRegisteredAppHandler,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("user-id", result.Value?.User.Id);
        A.CallTo(() =>
                _getRegisteredAppHandler.GetItemAsync(
                    A<GetRegisteredAppQuery>.That.Matches(query => query.AppName == "testapp"),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
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
            _getRegisteredAppHandler,
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
            _getRegisteredAppHandler,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.Created, result.StatusCode);
        Assert.Same(_registeredApp, command.RegisteredApp);
        A.CallTo(() =>
                _getRegisteredAppHandler.GetItemAsync(
                    A<GetRegisteredAppQuery>.That.Matches(query => query.AppName == "signedapp"),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
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
            _getRegisteredAppHandler,
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
    public async Task Refresh_Token_Passes_The_Request_Context_Grants_Edit_Only_And_Answers_Refresh_Token_Generated()
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

        var httpContext = GetClientHttpContext();

        // Act
        var result = await UserEndpoints.ExchangeRefreshTokenAsync(
            command,
            ApiRequest.For(httpContext),
            httpContext,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.True(result.Value?.EditEnabled);
        Assert.False(result.Value?.DeleteEnabled);
        Assert.Equal("Refresh token generated successfully", result.Value?.StatusMessage);
        Assert.Equal(RequestContext.Create("203.0.113.7", "Test Browser"), command.RequestContext);
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
            _getRegisteredAppHandler,
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
    public async Task Resend_Code_Passes_The_Requests_App_And_Answers_Code_Resend_Requested()
    {
        // Arrange
        ResendCodeCommand command = new() { Ticket = "ticket" };
        var handler = A.Fake<ICreateItemCommandHandler<ResendCodeCommand, ResendCodeViewModel>>();
        A.CallTo(() => handler.CreateItemAsync(command, A<string>._, A<CancellationToken>._)).Returns(new ResendCodeViewModel());

        var request = ApiRequestFactory.Create("user", appName: "test-app");

        // Act
        var result = await UserEndpoints.ResendCodeAsync(
            command,
            request,
            _getRegisteredAppHandler,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Same(_registeredApp, command.RegisteredApp);
        Assert.Equal("Code resend requested", result.Value?.StatusMessage);
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
            _getRegisteredAppHandler,
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
            _getRegisteredAppHandler,
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
            _getRegisteredAppHandler,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("target-id", command.UserId);
        Assert.Same(_registeredApp, command.RegisteredApp);
        Assert.Equal(new UserChanger(TestStringHelper.UserId, false), command.Changer);
        Assert.Equal(RequestContext.Create("203.0.113.7", "Test Browser"), command.RequestContext);
        A.CallTo(() =>
                _getRegisteredAppHandler.GetItemAsync(
                    A<GetRegisteredAppQuery>.That.Matches(query => query.AppName == "testapp"),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
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
            _getRegisteredAppHandler,
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
            _getRegisteredAppHandler,
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
            _getRegisteredAppHandler,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([TwoFactorMethod.Authenticator], result.Value?.AllowedMethods);
        A.CallTo(() =>
                _getRegisteredAppHandler.GetItemAsync(
                    A<GetRegisteredAppQuery>.That.Matches(query => query.AppName == "testapp"),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
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
            _getRegisteredAppHandler,
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
            _getRegisteredAppHandler,
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
            _getRegisteredAppHandler,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Same(_registeredApp, command.RegisteredApp);
        Assert.Equal(new UserChanger(TestStringHelper.UserId, false), command.Changer);
    }

    [Fact]
    public async Task Get_Sessions_Passes_The_Target_The_Callers_App_Who_Asks_And_The_Callers_Session()
    {
        // Arrange
        var sessionId = Guid.CreateVersion7();
        var handler = A.Fake<IGetItemsQueryHandler<GetSessionsQuery, SessionsViewModel>>();

        A.CallTo(() => handler.GetItemsAsync(A<GetSessionsQuery>._, A<CancellationToken>._))
            .Returns(new SessionsViewModel { Sessions = [] });

        var httpContext = ApiRequestFactory.CreateHttpContext("user", userId: TestStringHelper.UserId, sessionId: sessionId);

        // Act
        var result = await UserEndpoints.GetSessionsAsync(
            TestStringHelper.UserId,
            ApiRequest.For(httpContext),
            httpContext,
            _getRegisteredAppHandler,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(ItemStatusMessage.FetchItemsSuccessful.ToText(), result.Value?.StatusMessage);
        A.CallTo(() =>
                _getRegisteredAppHandler.GetItemAsync(
                    A<GetRegisteredAppQuery>.That.Matches(query => query.AppName == "testapp"),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() =>
                handler.GetItemsAsync(
                    new GetSessionsQuery
                    {
                        UserId = TestStringHelper.UserId,
                        RegisteredApp = _registeredApp,
                        Changer = new UserChanger(TestStringHelper.UserId, false),
                        CurrentSessionId = sessionId,
                    },
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Get_Sessions_With_A_Token_Without_A_Session_Has_No_Current_Session()
    {
        // Arrange
        var handler = A.Fake<IGetItemsQueryHandler<GetSessionsQuery, SessionsViewModel>>();

        A.CallTo(() => handler.GetItemsAsync(A<GetSessionsQuery>._, A<CancellationToken>._))
            .Returns(new SessionsViewModel { Sessions = [] });

        var httpContext = ApiRequestFactory.CreateHttpContext("user", userId: "admin-id", roles: ["testapp:admin"]);

        // Act
        await UserEndpoints.GetSessionsAsync(
            "target-id",
            ApiRequest.For(httpContext),
            httpContext,
            _getRegisteredAppHandler,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        A.CallTo(() =>
                handler.GetItemsAsync(
                    A<GetSessionsQuery>.That.Matches(query =>
                        query.UserId == "target-id" && query.Changer == new UserChanger("admin-id", false) && query.CurrentSessionId == null
                    ),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task End_Session_Passes_The_Target_The_Session_The_Callers_App_And_Who_Ends_It()
    {
        // Arrange
        var sessionId = Guid.CreateVersion7();
        var handler = A.Fake<IDeleteItemCommandHandler<EndSessionCommand, DeleteRecordViewModel>>();

        A.CallTo(() => handler.DeleteItemAsync(A<EndSessionCommand>._, A<string>._, A<CancellationToken>._))
            .Returns(new DeleteRecordViewModel());

        var request = ApiRequestFactory.Create("user", userId: TestStringHelper.UserId);

        // Act
        var result = await UserEndpoints.EndSessionAsync(
            TestStringHelper.UserId,
            sessionId,
            request,
            _getRegisteredAppHandler,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(ItemStatusMessage.DeleteItemSuccessful.ToText(), result.Value?.StatusMessage);
        A.CallTo(() =>
                handler.DeleteItemAsync(
                    new EndSessionCommand(
                        TestStringHelper.UserId,
                        sessionId,
                        _registeredApp,
                        new UserChanger(TestStringHelper.UserId, false)
                    ),
                    TestStringHelper.UserId,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task End_Sessions_Passes_The_Callers_Session_To_Keep()
    {
        // Arrange
        var sessionId = Guid.CreateVersion7();
        var handler = A.Fake<IDeleteItemCommandHandler<EndSessionsCommand, DeleteRecordViewModel>>();

        A.CallTo(() => handler.DeleteItemAsync(A<EndSessionsCommand>._, A<string>._, A<CancellationToken>._))
            .Returns(new DeleteRecordViewModel());

        var httpContext = ApiRequestFactory.CreateHttpContext("user", userId: TestStringHelper.UserId, sessionId: sessionId);

        // Act
        var result = await UserEndpoints.EndSessionsAsync(
            TestStringHelper.UserId,
            ApiRequest.For(httpContext),
            httpContext,
            _getRegisteredAppHandler,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        A.CallTo(() =>
                handler.DeleteItemAsync(
                    new EndSessionsCommand(
                        TestStringHelper.UserId,
                        _registeredApp,
                        new UserChanger(TestStringHelper.UserId, false),
                        sessionId
                    ),
                    TestStringHelper.UserId,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Delete_User_Passes_The_Target_The_Callers_App_Who_Deletes_And_The_Request_Context()
    {
        // Arrange
        DeleteUserCommand command = new() { Password = "password" };
        var handler = A.Fake<IDeleteItemCommandHandler<DeleteUserCommand, DeleteRecordViewModel>>();

        A.CallTo(() => handler.DeleteItemAsync(command, A<string>._, A<CancellationToken>._)).Returns(new DeleteRecordViewModel());

        var httpContext = GetClientHttpContext(userId: TestStringHelper.UserId);

        // Act
        var result = await UserEndpoints.DeleteUserAsync(
            TestStringHelper.UserId,
            command,
            ApiRequest.For(httpContext),
            httpContext,
            _getRegisteredAppHandler,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(ItemStatusMessage.DeleteItemSuccessful.ToText(), result.Value?.StatusMessage);
        Assert.Equal(TestStringHelper.UserId, command.UserId);
        Assert.Same(_registeredApp, command.RegisteredApp);
        Assert.Equal(new UserChanger(TestStringHelper.UserId, false), command.Changer);
        Assert.Equal(_registeredApp.Id, command.ChangerAppId);
        Assert.Equal(RequestContext.Create("203.0.113.7", "Test Browser"), command.RequestContext);
        A.CallTo(() => handler.DeleteItemAsync(command, TestStringHelper.UserId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Delete_User_By_A_Global_Admin_Without_A_Body_Uses_The_Named_App_And_No_Password()
    {
        // Arrange
        var handler = A.Fake<IDeleteItemCommandHandler<DeleteUserCommand, DeleteRecordViewModel>>();

        A.CallTo(() => handler.DeleteItemAsync(A<DeleteUserCommand>._, A<string>._, A<CancellationToken>._))
            .Returns(new DeleteRecordViewModel());

        var httpContext = ApiRequestFactory.CreateHttpContext("user", userId: "admin-id", appName: "test-app", roles: ["global:admin"]);

        // Act
        await UserEndpoints.DeleteUserAsync(
            "target-id",
            null,
            ApiRequest.For(httpContext),
            httpContext,
            _getRegisteredAppHandler,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        A.CallTo(() =>
                _getRegisteredAppHandler.GetItemAsync(
                    A<GetRegisteredAppQuery>.That.Matches(query => query.AppName == "test-app"),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() =>
                handler.DeleteItemAsync(
                    A<DeleteUserCommand>.That.Matches(c =>
                        c.UserId == "target-id"
                        && c.Password == null
                        && c.Ticket == null
                        && c.RegisteredApp == _registeredApp
                        && c.Changer == new UserChanger("admin-id", true)
                    ),
                    "admin-id",
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Delete_User_Passes_The_Ticket_With_The_App_Of_The_Callers_Token()
    {
        // Arrange
        DeleteUserCommand command = new() { Ticket = "ticket" };
        var handler = A.Fake<IDeleteItemCommandHandler<DeleteUserCommand, DeleteRecordViewModel>>();
        var tokenApp = new RegisteredAppDto
        {
            Id = 7,
            AppName = "testapp",
            BaseUrl = "https://token.app.test",
        };

        A.CallTo(() =>
                _getRegisteredAppHandler.GetItemAsync(
                    A<GetRegisteredAppQuery>.That.Matches(query => query.AppName == "testapp"),
                    A<CancellationToken>._
                )
            )
            .Returns(new RegisteredAppViewModel { RegisteredApp = tokenApp });
        A.CallTo(() => handler.DeleteItemAsync(command, A<string>._, A<CancellationToken>._)).Returns(new DeleteRecordViewModel());

        var httpContext = ApiRequestFactory.CreateHttpContext("user", userId: "admin-id", appName: "test-app", roles: ["global:admin"]);

        // Act
        await UserEndpoints.DeleteUserAsync(
            "target-id",
            command,
            ApiRequest.For(httpContext),
            httpContext,
            _getRegisteredAppHandler,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal("ticket", command.Ticket);
        Assert.Same(_registeredApp, command.RegisteredApp);
        Assert.Equal(7, command.ChangerAppId);
    }

    [Fact]
    public async Task Get_Users_Passes_The_Admins_App_Who_Asks_And_The_Filters()
    {
        // Arrange
        var handler = A.Fake<IGetItemsQueryHandler<GetUsersQuery, UsersViewModel>>();

        A.CallTo(() => handler.GetItemsAsync(A<GetUsersQuery>._, A<CancellationToken>._))
            .Returns(new UsersViewModel { Users = [], TotalCount = 0 });

        var request = ApiRequestFactory.Create("user", userId: "admin-id", appName: "test-app", roles: ["global:admin"]);

        // Act
        var result = await UserEndpoints.GetUsersAsync(
            request,
            _getRegisteredAppHandler,
            handler,
            TestContext.Current.CancellationToken,
            page: 2,
            pageSize: 10,
            search: "test",
            role: "test-app:owner",
            emailConfirmed: false
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(ItemStatusMessage.FetchItemsSuccessful.ToText(), result.Value?.StatusMessage);
        A.CallTo(() =>
                _getRegisteredAppHandler.GetItemAsync(
                    A<GetRegisteredAppQuery>.That.Matches(query => query.AppName == "test-app"),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() =>
                handler.GetItemsAsync(
                    new GetUsersQuery
                    {
                        RegisteredApp = _registeredApp,
                        Changer = new UserChanger("admin-id", true),
                        Search = "test",
                        Role = "test-app:owner",
                        EmailConfirmed = false,
                        Page = 2,
                        PageSize = 10,
                    },
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Send_Step_Up_Code_Is_For_The_Callers_Own_Account_In_The_Tokens_App()
    {
        // Arrange
        var handler = A.Fake<ICreateItemCommandHandler<SendStepUpCodeCommand, StepUpCodeViewModel>>();

        A.CallTo(() => handler.CreateItemAsync(A<SendStepUpCodeCommand>._, A<string>._, A<CancellationToken>._))
            .Returns(new StepUpCodeViewModel { Method = TwoFactorMethod.Email, CodeSentTo = "a***@test.com" });

        var httpContext = GetClientHttpContext(userId: "admin-id");

        // Act
        var result = await UserEndpoints.SendStepUpCodeAsync(
            ApiRequest.For(httpContext),
            httpContext,
            _getRegisteredAppHandler,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("a***@test.com", result.Value?.CodeSentTo);
        A.CallTo(() =>
                _getRegisteredAppHandler.GetItemAsync(
                    A<GetRegisteredAppQuery>.That.Matches(query => query.AppName == "testapp"),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() =>
                handler.CreateItemAsync(
                    new SendStepUpCodeCommand("admin-id", _registeredApp, RequestContext.Create("203.0.113.7", "Test Browser")),
                    "admin-id",
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Create_Step_Up_Is_For_The_Callers_Own_Account_In_The_Tokens_App()
    {
        // Arrange
        CreateStepUpCommand command = new() { Code = "123456" };
        var handler = A.Fake<ICreateItemCommandHandler<CreateStepUpCommand, StepUpViewModel>>();

        A.CallTo(() => handler.CreateItemAsync(command, A<string>._, A<CancellationToken>._))
            .Returns(new StepUpViewModel { Ticket = "ticket", ExpiresAt = DateTime.UtcNow.AddMinutes(5) });

        var request = ApiRequestFactory.Create("user", userId: "admin-id", appName: "test-app", roles: ["global:admin"]);

        // Act
        var result = await UserEndpoints.CreateStepUpAsync(
            command,
            request,
            _getRegisteredAppHandler,
            handler,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("ticket", result.Value?.Ticket);
        Assert.Equal("admin-id", command.UserId);
        Assert.Same(_registeredApp, command.RegisteredApp);
        A.CallTo(() =>
                _getRegisteredAppHandler.GetItemAsync(
                    A<GetRegisteredAppQuery>.That.Matches(query => query.AppName == "testapp"),
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => handler.CreateItemAsync(command, "admin-id", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
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
