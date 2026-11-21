using System.Security.Authentication;
using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Settings;
using PolyzenKit.Domain.Entities.RegisteredApps;
using UserIdentity.Application.Core.AccountLimits.Queries;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Application.Core.SignInSettings.Queries;
using UserIdentity.Application.Core.SignInSettings.ViewModels;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Common.Enums;
using UserIdentity.Domain.AppSignInSettings;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.Users;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class SignInUserCommandHandlerTests
{
    private static readonly DateTime _ticketExpires = new(2026, 11, 11, 12, 10, 0, DateTimeKind.Utc);

    private readonly UserManager<IdentityUser> _userManager = IdentityFakes.UserManager();
    private readonly IUserRepository _userRepository = A.Fake<IUserRepository>();
    private readonly IGetItemQueryHandler<GetSignInSettingsQuery, SignInSettingsViewModel> _getSignInSettingsQueryHandler = A.Fake<
        IGetItemQueryHandler<GetSignInSettingsQuery, SignInSettingsViewModel>
    >();
    private readonly IGetItemQueryHandler<GetAccountLimitQuery, AccountLimitViewModel> _getAccountLimitQueryHandler = A.Fake<
        IGetItemQueryHandler<GetAccountLimitQuery, AccountLimitViewModel>
    >();
    private readonly ICreateItemCommandHandler<SendLoginCodeCommand, LoginCodeViewModel> _sendLoginCodeCommandHandler = A.Fake<
        ICreateItemCommandHandler<SendLoginCodeCommand, LoginCodeViewModel>
    >();
    private readonly ICreateItemCommandHandler<StartSessionCommand, UserTokenViewModel> _startSessionCommandHandler = A.Fake<
        ICreateItemCommandHandler<StartSessionCommand, UserTokenViewModel>
    >();
    private readonly ICreateItemCommandHandler<CreateLoginTicketCommand, LoginTicketViewModel> _createLoginTicketCommandHandler = A.Fake<
        ICreateItemCommandHandler<CreateLoginTicketCommand, LoginTicketViewModel>
    >();

    private readonly RegisteredAppDto _registeredApp = new()
    {
        Id = 1,
        AppName = "TestApp",
        BaseUrl = "https://app.test",
    };

    private readonly IdentityUser _user = new()
    {
        Id = "user-1",
        UserName = "test",
        Email = "test@example.com",
        PhoneNumber = "+254700000123",
        EmailConfirmed = true,
    };

    private readonly RequestContext _requestContext = RequestContext.Create("203.0.113.7", "Test Browser");

    private readonly UserDto _userDto;
    private readonly UserTokenDto _userToken = new()
    {
        AccessToken = new AccessTokenDto { Token = "access-token" },
        RefreshToken = "refresh",
    };

    private AppSignInSettingsDto _signInSettings = new() { AppId = 1 };
    private List<string> _roles = [];

    public SignInUserCommandHandlerTests()
    {
        _userDto = new UserDto { Id = _user.Id, UserName = _user.UserName };

        A.CallTo(() => _userRepository.GetUserDtoAsync(_user.Id, _registeredApp.AppName, A<CancellationToken>._)).Returns(_userDto);
        A.CallTo(() => _getSignInSettingsQueryHandler.GetItemAsync(A<GetSignInSettingsQuery>._, A<CancellationToken>._))
            .ReturnsLazily(() => new SignInSettingsViewModel { SignInSettings = _signInSettings });
        A.CallTo(() => _userManager.GetRolesAsync(_user)).ReturnsLazily(() => _roles);
        A.CallTo(() => _userManager.UpdateAsync(_user)).Returns(IdentityResult.Success);
        A.CallTo(() => _userManager.ResetAccessFailedCountAsync(_user)).Returns(IdentityResult.Success);
        A.CallTo(() => _getAccountLimitQueryHandler.GetItemAsync(A<GetAccountLimitQuery>._, A<CancellationToken>._))
            .Returns(new AccountLimitViewModel { IsReached = false });
        A.CallTo(() => _sendLoginCodeCommandHandler.CreateItemAsync(A<SendLoginCodeCommand>._, A<string>._, A<CancellationToken>._))
            .ReturnsLazily(
                (SendLoginCodeCommand command, string _, CancellationToken _) =>
                    new LoginCodeViewModel { Sent = true, CodeSentTo = ContactMask.For(command.Email, command.PhoneNumber) }
            );
        A.CallTo(() => _startSessionCommandHandler.CreateItemAsync(A<StartSessionCommand>._, A<string>._, A<CancellationToken>._))
            .Returns(new UserTokenViewModel { UserToken = _userToken });
        A.CallTo(() => _createLoginTicketCommandHandler.CreateItemAsync(A<CreateLoginTicketCommand>._, A<string>._, A<CancellationToken>._))
            .Returns(new LoginTicketViewModel { Ticket = "ticket", Expires = _ticketExpires });
    }

    [Fact]
    public async Task Rule_1_An_Unconfirmed_Account_Of_An_App_That_Requires_Confirmation_Gets_ConfirmEmail_With_A_Login_Code()
    {
        // Arrange
        _signInSettings = new AppSignInSettingsDto { AppId = 1, RequireConfirmedAccount = true };
        _user.EmailConfirmed = false;

        // Act
        var vm = await GetHandler().CreateItemAsync(GetCommand(), string.Empty, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SignInStep.ConfirmEmail, vm.NextStep);
        Assert.Equal("ticket", vm.Ticket);
        Assert.Equal("t***@example.com", vm.CodeSentTo);
        Assert.Null(vm.UserToken);
        AssertLoginCodeSent(LoginCodePurpose.ConfirmEmail, _user.Email, null);
        AssertTicketCreated(SignInStep.ConfirmEmail, UserMessages.LoginCodeAction, emailProven: false);
        AssertNoSession();
    }

    [Fact]
    public async Task Rule_2_A_User_With_A_Method_Who_Passed_The_Second_Step_Gets_A_Session()
    {
        // Arrange
        UseMethod(TwoFactorMethod.Authenticator);

        // Act
        var vm = await GetHandler()
            .CreateItemAsync(GetCommand() with { SecondFactorPassed = true }, string.Empty, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SignInStep.Session, vm.NextStep);
        Assert.Same(_userToken, vm.UserToken);
        A.CallTo(() => _userManager.ResetAccessFailedCountAsync(_user)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Rule_3_A_User_Whose_Method_Is_Email_And_Who_Proved_The_Email_Gets_A_Session()
    {
        // Arrange
        UseMethod(TwoFactorMethod.Email);

        // Act
        var vm = await GetHandler()
            .CreateItemAsync(GetCommand() with { EmailProven = true }, string.Empty, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SignInStep.Session, vm.NextStep);
        A.CallTo(() => _sendLoginCodeCommandHandler.CreateItemAsync(A<SendLoginCodeCommand>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(TwoFactorMethod.Authenticator, null)]
    [InlineData(TwoFactorMethod.Email, "t***@example.com")]
    [InlineData(TwoFactorMethod.Sms, "***123")]
    public async Task Rule_4_A_User_With_A_Method_Gets_SecondFactor_And_A_Code_For_Email_And_Text(
        TwoFactorMethod method,
        string? codeSentTo
    )
    {
        // Arrange
        UseMethod(method);

        // Act
        var vm = await GetHandler().CreateItemAsync(GetCommand(), string.Empty, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SignInStep.SecondFactor, vm.NextStep);
        Assert.Equal(method, vm.Method);
        Assert.Equal(codeSentTo, vm.CodeSentTo);
        Assert.Equal("ticket", vm.Ticket);
        AssertNoSession();

        if (method == TwoFactorMethod.Authenticator)
        {
            A.CallTo(() => _sendLoginCodeCommandHandler.CreateItemAsync(A<SendLoginCodeCommand>._, A<string>._, A<CancellationToken>._))
                .MustNotHaveHappened();
            AssertTicketCreated(SignInStep.SecondFactor, null, emailProven: false);
        }
        else
        {
            AssertLoginCodeSent(
                LoginCodePurpose.SignIn,
                method == TwoFactorMethod.Email ? _user.Email : null,
                method == TwoFactorMethod.Sms ? _user.PhoneNumber : null
            );
            AssertTicketCreated(SignInStep.SecondFactor, UserMessages.LoginCodeAction, emailProven: false);
        }
    }

    [Fact]
    public async Task Rule_4_Past_The_Failed_Second_Step_Cap_Answers_The_Lockout()
    {
        // Arrange
        UseMethod(TwoFactorMethod.Authenticator);
        A.CallTo(() =>
                _getAccountLimitQueryHandler.GetItemAsync(
                    new GetAccountLimitQuery(_user.Id, AccountLimitKind.FailedSecondStep),
                    A<CancellationToken>._
                )
            )
            .Returns(new AccountLimitViewModel { IsReached = true });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialException>(() =>
            GetHandler().CreateItemAsync(GetCommand(), string.Empty, TestContext.Current.CancellationToken)
        );

        AssertNoSession();
    }

    [Fact]
    public async Task Rule_4_Past_The_Login_Code_Cap_Answers_The_Lockout()
    {
        // Arrange
        UseMethod(TwoFactorMethod.Email);
        A.CallTo(() => _sendLoginCodeCommandHandler.CreateItemAsync(A<SendLoginCodeCommand>._, A<string>._, A<CancellationToken>._))
            .Returns(new LoginCodeViewModel { Sent = false });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialException>(() =>
            GetHandler().CreateItemAsync(GetCommand(), string.Empty, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Rule_5_A_Required_Account_Without_A_Method_And_A_Proved_Email_Gets_SetUpSecondFactor()
    {
        // Arrange
        _signInSettings = new AppSignInSettingsDto
        {
            AppId = 1,
            RequireTwoFactor = true,
            AllowAuthenticatorTwoFactor = true,
            AllowSmsTwoFactor = true,
        };

        // Act
        var vm = await GetHandler()
            .CreateItemAsync(GetCommand() with { EmailProven = true }, string.Empty, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SignInStep.SetUpSecondFactor, vm.NextStep);
        Assert.Equal([TwoFactorMethod.Authenticator, TwoFactorMethod.Sms], vm.AllowedMethods);
        AssertTicketCreated(SignInStep.SetUpSecondFactor, null, emailProven: true);
        AssertNoSession();
    }

    [Fact]
    public async Task Rule_6_A_Required_Account_Without_A_Method_Or_A_Proved_Email_Gets_ConfirmEmail_With_A_Login_Code()
    {
        // Arrange
        _signInSettings = new AppSignInSettingsDto
        {
            AppId = 1,
            RequireTwoFactorForAdmins = true,
            AllowEmailTwoFactor = true,
        };
        _roles = ["testapp:admin"];

        // Act
        var vm = await GetHandler().CreateItemAsync(GetCommand(), string.Empty, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SignInStep.ConfirmEmail, vm.NextStep);
        AssertLoginCodeSent(LoginCodePurpose.ConfirmEmail, _user.Email, null);
        AssertNoSession();
    }

    [Fact]
    public async Task Rule_7_Anyone_Else_Gets_A_Session()
    {
        // Arrange
        _signInSettings = new AppSignInSettingsDto
        {
            AppId = 1,
            RequireTwoFactorForAdmins = true,
            AllowEmailTwoFactor = true,
        };
        _roles = ["testapp:user"];

        // Act
        var vm = await GetHandler().CreateItemAsync(GetCommand(), string.Empty, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SignInStep.Session, vm.NextStep);
        Assert.Same(_userToken, vm.UserToken);
        A.CallTo(() =>
                _startSessionCommandHandler.CreateItemAsync(
                    new StartSessionCommand(_user.Id, _user.UserName!, _registeredApp, _requestContext),
                    _user.Id,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task A_Method_The_App_No_Longer_Allows_Counts_As_No_Method()
    {
        // Arrange
        _signInSettings = new AppSignInSettingsDto { AppId = 1, AllowAuthenticatorTwoFactor = true };
        _userDto.TwoFactorMethod = TwoFactorMethod.Sms;

        // Act
        var vm = await GetHandler().CreateItemAsync(GetCommand(), string.Empty, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SignInStep.Session, vm.NextStep);
    }

    [Fact]
    public async Task A_Proved_Email_Is_Marked_Confirmed()
    {
        // Arrange
        _user.EmailConfirmed = false;

        // Act
        await GetHandler().CreateItemAsync(GetCommand() with { EmailProven = true }, string.Empty, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(_user.EmailConfirmed);
        A.CallTo(() => _userManager.UpdateAsync(_user)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Only_A_Session_Resets_The_Failure_Count()
    {
        // Arrange
        UseMethod(TwoFactorMethod.Authenticator);

        // Act
        await GetHandler().CreateItemAsync(GetCommand(), string.Empty, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _userManager.ResetAccessFailedCountAsync(A<IdentityUser>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Changing_The_Method_Asks_For_The_Email_First_Then_Sets_Up()
    {
        // Arrange
        UseMethod(TwoFactorMethod.Email);

        // Act
        var confirmEmail = await GetHandler()
            .CreateItemAsync(GetCommand() with { ChangingMethod = true }, string.Empty, TestContext.Current.CancellationToken);
        var setUp = await GetHandler()
            .CreateItemAsync(
                GetCommand() with
                {
                    ChangingMethod = true,
                    EmailProven = true,
                },
                string.Empty,
                TestContext.Current.CancellationToken
            );

        // Assert
        Assert.Equal(SignInStep.ConfirmEmail, confirmEmail.NextStep);
        Assert.Equal(SignInStep.SetUpSecondFactor, setUp.NextStep);
        A.CallTo(() =>
                _createLoginTicketCommandHandler.CreateItemAsync(
                    A<CreateLoginTicketCommand>.That.Matches(c => c.Step == SignInStep.ConfirmEmail && c.ChangingMethod),
                    A<string>._,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        AssertNoSession();
    }

    [Fact]
    public async Task Sign_In_Without_A_Profile_For_The_App_Throws_InvalidCredentialException()
    {
        // Arrange
        A.CallTo(() => _userRepository.GetUserDtoAsync(_user.Id, _registeredApp.AppName, A<CancellationToken>._)).Returns((UserDto?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialException>(() =>
            GetHandler().CreateItemAsync(GetCommand(), string.Empty, TestContext.Current.CancellationToken)
        );

        AssertNoSession();
    }

    private void UseMethod(TwoFactorMethod method)
    {
        _signInSettings = new AppSignInSettingsDto
        {
            AppId = 1,
            AllowAuthenticatorTwoFactor = true,
            AllowEmailTwoFactor = true,
            AllowSmsTwoFactor = true,
        };
        _userDto.TwoFactorMethod = method;
    }

    private void AssertLoginCodeSent(LoginCodePurpose purpose, string? email, string? phoneNumber) =>
        A.CallTo(() =>
                _sendLoginCodeCommandHandler.CreateItemAsync(
                    A<SendLoginCodeCommand>.That.Matches(c =>
                        c.Purpose == purpose
                        && c.Email == email
                        && c.PhoneNumber == phoneNumber
                        && c.ExpiresAt == _ticketExpires
                        && c.AppName == _registeredApp.AppName
                        && c.UserId == _user.Id
                    ),
                    _user.Id,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();

    private void AssertTicketCreated(SignInStep step, string? codeAction, bool emailProven) =>
        A.CallTo(() =>
                _createLoginTicketCommandHandler.CreateItemAsync(
                    A<CreateLoginTicketCommand>.That.Matches(c =>
                        c.Step == step
                        && c.CodeAction == codeAction
                        && (codeAction == null ? c.CodeCorrelationId == null : c.CodeCorrelationId == c.Id.ToString())
                        && c.EmailProven == emailProven
                        && c.UserId == _user.Id
                        && c.AppId == _registeredApp.Id
                    ),
                    _user.Id,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();

    private void AssertNoSession() =>
        A.CallTo(() => _startSessionCommandHandler.CreateItemAsync(A<StartSessionCommand>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();

    private SignInUserCommand GetCommand() => new(_user, _registeredApp, _requestContext);

    private SignInUserCommandHandler GetHandler() =>
        new(
            Options.Create(new RoleSettings { ServiceName = "useridentity" }),
            _userManager,
            _userRepository,
            _getSignInSettingsQueryHandler,
            _getAccountLimitQueryHandler,
            _sendLoginCodeCommandHandler,
            _startSessionCommandHandler,
            _createLoginTicketCommandHandler
        );
}
