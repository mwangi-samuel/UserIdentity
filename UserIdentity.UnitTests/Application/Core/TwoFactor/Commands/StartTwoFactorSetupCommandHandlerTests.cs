using System.Security.Authentication;
using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.SignInSettings.Queries;
using UserIdentity.Application.Core.SignInSettings.ViewModels;
using UserIdentity.Application.Core.TwoFactor.Commands;
using UserIdentity.Application.Core.TwoFactor.Queries;
using UserIdentity.Application.Core.TwoFactor.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.AppSignInSettings;
using UserIdentity.Domain.LoginTickets;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.LoginTickets;
using UserIdentity.Persistence.Repositories.Users;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.TwoFactor.Commands;

public class StartTwoFactorSetupCommandHandlerTests
{
    private static readonly DateTime _now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private readonly UserManager<IdentityUser> _userManager = IdentityFakes.UserManager();
    private readonly IUnitOfWork _unitOfWork = A.Fake<IUnitOfWork>();
    private readonly IUserRepository _userRepository = A.Fake<IUserRepository>();
    private readonly ILoginTicketRepository _loginTicketRepository = A.Fake<ILoginTicketRepository>();
    private readonly IGetItemQueryHandler<GetSecondStepTicketQuery, SecondStepTicketViewModel> _getSecondStepTicketQueryHandler = A.Fake<
        IGetItemQueryHandler<GetSecondStepTicketQuery, SecondStepTicketViewModel>
    >();
    private readonly IGetItemQueryHandler<GetSignInSettingsQuery, SignInSettingsViewModel> _getSignInSettingsQueryHandler = A.Fake<
        IGetItemQueryHandler<GetSignInSettingsQuery, SignInSettingsViewModel>
    >();
    private readonly ICreateItemCommandHandler<SendLoginCodeCommand, LoginCodeViewModel> _sendLoginCodeCommandHandler = A.Fake<
        ICreateItemCommandHandler<SendLoginCodeCommand, LoginCodeViewModel>
    >();

    private readonly RegisteredAppDto _registeredApp = new()
    {
        Id = 1,
        AppName = "Test App",
        BaseUrl = "https://app.test",
    };

    private readonly IdentityUser _user = new()
    {
        Id = "user-1",
        UserName = "test",
        Email = "test@example.com",
    };

    private readonly LoginTicketEntity _loginTicket = new()
    {
        Id = Guid.CreateVersion7(),
        UserId = "user-1",
        Step = SignInStep.SetUpSecondFactor,
        Expires = _now.AddMinutes(10),
        EmailProven = true,
    };

    public StartTwoFactorSetupCommandHandlerTests()
    {
        A.CallTo(() => _getSecondStepTicketQueryHandler.GetItemAsync(A<GetSecondStepTicketQuery>._, A<CancellationToken>._))
            .Returns(new SecondStepTicketViewModel { LoginTicket = _loginTicket, User = _user });
        A.CallTo(() => _getSignInSettingsQueryHandler.GetItemAsync(A<GetSignInSettingsQuery>._, A<CancellationToken>._))
            .Returns(
                new SignInSettingsViewModel
                {
                    SignInSettings = new AppSignInSettingsDto
                    {
                        AppId = 1,
                        AllowAuthenticatorTwoFactor = true,
                        AllowEmailTwoFactor = true,
                        AllowSmsTwoFactor = true,
                    },
                }
            );
        A.CallTo(() => _userRepository.GetUserDtoAsync(_user.Id, _registeredApp.AppName, A<CancellationToken>._))
            .Returns(new UserDto { Id = _user.Id, FirstName = "Test" });
        A.CallTo(() => _sendLoginCodeCommandHandler.CreateItemAsync(A<SendLoginCodeCommand>._, A<string>._, A<CancellationToken>._))
            .Returns(new LoginCodeViewModel { Sent = true, CodeSentTo = "***123" });
    }

    [Fact]
    public async Task Setting_Up_An_Authenticator_Answers_A_New_Key_And_Its_Link()
    {
        // Arrange
        A.CallTo(() => _userManager.ResetAuthenticatorKeyAsync(_user)).Returns(IdentityResult.Success);
        A.CallTo(() => _userManager.GetAuthenticatorKeyAsync(_user)).Returns("JBSWY3DPEHPK3PXP");

        // Act
        var vm = await GetHandler()
            .CreateItemAsync(GetCommand(TwoFactorMethod.Authenticator), string.Empty, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TwoFactorMethod.Authenticator, vm.Method);
        Assert.Equal("JBSWY3DPEHPK3PXP", vm.AuthenticatorKey);
        Assert.Equal(
            "otpauth://totp/Test%20App:test%40example.com?secret=JBSWY3DPEHPK3PXP&issuer=Test%20App&digits=6",
            vm.AuthenticatorUri
        );
        Assert.Equal(TwoFactorMethod.Authenticator, _loginTicket.Method);
        Assert.Null(_loginTicket.CodeAction);
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Setting_Up_Email_Needs_No_Code()
    {
        // Act
        var vm = await GetHandler().CreateItemAsync(GetCommand(TwoFactorMethod.Email), string.Empty, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TwoFactorMethod.Email, vm.Method);
        Assert.Null(vm.CodeSentTo);
        Assert.Equal(TwoFactorMethod.Email, _loginTicket.Method);
        A.CallTo(() => _sendLoginCodeCommandHandler.CreateItemAsync(A<SendLoginCodeCommand>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Setting_Up_Text_Messages_Sends_A_Code_To_That_Phone_Only()
    {
        // Act
        var vm = await GetHandler()
            .CreateItemAsync(GetCommand(TwoFactorMethod.Sms, " +254700000123 "), string.Empty, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TwoFactorMethod.Sms, vm.Method);
        Assert.Equal("***123", vm.CodeSentTo);
        Assert.Equal("+254700000123", _loginTicket.PhoneNumber);
        Assert.Equal(UserMessages.LoginCodeAction, _loginTicket.CodeAction);
        Assert.Equal(_loginTicket.Id.ToString(), _loginTicket.CodeCorrelationId);
        A.CallTo(() =>
                _sendLoginCodeCommandHandler.CreateItemAsync(
                    A<SendLoginCodeCommand>.That.Matches(c =>
                        c.Purpose == LoginCodePurpose.SetUp
                        && c.PhoneNumber == "+254700000123"
                        && c.Email == null
                        && c.CorrelationId == _loginTicket.Id.ToString()
                        && c.ExpiresAt == _loginTicket.Expires
                    ),
                    _user.Id,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Setting_Up_Text_Messages_Past_The_Login_Code_Cap_Answers_The_Lockout()
    {
        // Arrange
        A.CallTo(() => _sendLoginCodeCommandHandler.CreateItemAsync(A<SendLoginCodeCommand>._, A<string>._, A<CancellationToken>._))
            .Returns(new LoginCodeViewModel { Sent = false });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialException>(() =>
            GetHandler()
                .CreateItemAsync(GetCommand(TwoFactorMethod.Sms, "+254700000123"), string.Empty, TestContext.Current.CancellationToken)
        );

        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task A_Method_The_App_Does_Not_Allow_Is_Refused()
    {
        // Arrange
        A.CallTo(() => _getSignInSettingsQueryHandler.GetItemAsync(A<GetSignInSettingsQuery>._, A<CancellationToken>._))
            .Returns(
                new SignInSettingsViewModel
                {
                    SignInSettings = new AppSignInSettingsDto { AppId = 1, AllowEmailTwoFactor = true },
                }
            );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler()
                .CreateItemAsync(GetCommand(TwoFactorMethod.Sms, "+254700000123"), string.Empty, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.TwoFactor.MethodNotAllowed, exception.Message);
    }

    private StartTwoFactorSetupCommand GetCommand(TwoFactorMethod method, string? phoneNumber = null) =>
        new()
        {
            Ticket = "ticket",
            Method = method,
            PhoneNumber = phoneNumber,
            RegisteredApp = _registeredApp,
        };

    private StartTwoFactorSetupCommandHandler GetHandler() =>
        new(
            _userManager,
            _unitOfWork,
            _userRepository,
            _loginTicketRepository,
            _getSecondStepTicketQueryHandler,
            _getSignInSettingsQueryHandler,
            _sendLoginCodeCommandHandler,
            new FakeTimeProvider(_now)
        );
}
