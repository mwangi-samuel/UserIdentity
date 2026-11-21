using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Application.Core.SignInSettings.Queries;
using UserIdentity.Application.Core.SignInSettings.ViewModels;
using UserIdentity.Application.Core.TwoFactor.Commands;
using UserIdentity.Application.Core.TwoFactor.Queries;
using UserIdentity.Application.Core.TwoFactor.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Application.Interfaces;
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

public class VerifySecondFactorCommandHandlerTests
{
    private readonly UserManager<IdentityUser> _userManager = IdentityFakes.UserManager();
    private readonly IUnitOfWork _unitOfWork = A.Fake<IUnitOfWork>();
    private readonly IUserRepository _userRepository = A.Fake<IUserRepository>();
    private readonly ILoginTicketRepository _loginTicketRepository = A.Fake<ILoginTicketRepository>();
    private readonly ICodeCheckService _codeCheckService = A.Fake<ICodeCheckService>();
    private readonly IGetItemQueryHandler<GetSecondStepTicketQuery, SecondStepTicketViewModel> _getSecondStepTicketQueryHandler = A.Fake<
        IGetItemQueryHandler<GetSecondStepTicketQuery, SecondStepTicketViewModel>
    >();
    private readonly IGetItemQueryHandler<GetSignInSettingsQuery, SignInSettingsViewModel> _getSignInSettingsQueryHandler = A.Fake<
        IGetItemQueryHandler<GetSignInSettingsQuery, SignInSettingsViewModel>
    >();
    private readonly ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel> _recordFailedSignInCommandHandler = A.Fake<
        ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel>
    >();
    private readonly ICreateItemCommandHandler<SignInUserCommand, AuthUserViewModel> _signInUserCommandHandler = A.Fake<
        ICreateItemCommandHandler<SignInUserCommand, AuthUserViewModel>
    >();

    private readonly ActiveRegisteredApp _registeredApp = new()
    {
        Id = 1,
        AppName = "TestApp",
        BaseUrl = "https://app.test",
    };

    private readonly IdentityUser _user = new() { Id = "user-1", UserName = "test" };
    private readonly UserDto _userDto = new() { Id = "user-1" };

    private readonly LoginTicketEntity _loginTicket = new()
    {
        Id = Guid.CreateVersion7(),
        UserId = "user-1",
        Step = SignInStep.SecondFactor,
    };

    public VerifySecondFactorCommandHandlerTests()
    {
        A.CallTo(() => _getSecondStepTicketQueryHandler.GetItemAsync(A<GetSecondStepTicketQuery>._, A<CancellationToken>._))
            .Returns(new SecondStepTicketViewModel { LoginTicket = _loginTicket, User = _user });
        A.CallTo(() => _userRepository.GetUserDtoAsync(_user.Id, _registeredApp.AppName, A<CancellationToken>._)).Returns(_userDto);
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
        A.CallTo(() => _signInUserCommandHandler.CreateItemAsync(A<SignInUserCommand>._, A<string>._, A<CancellationToken>._))
            .Returns(new AuthUserViewModel { User = _userDto, NextStep = SignInStep.Session });
    }

    [Fact]
    public async Task A_Right_Authenticator_Code_Uses_Up_The_Ticket_And_Signs_In()
    {
        // Arrange
        _userDto.TwoFactorMethod = TwoFactorMethod.Authenticator;
        A.CallTo(() => _userManager.VerifyTwoFactorTokenAsync(_user, TokenOptions.DefaultAuthenticatorProvider, "123456")).Returns(true);

        // Act
        var vm = await GetHandler().CreateItemAsync(GetCommand(code: "123 456"), string.Empty, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SignInStep.Session, vm.NextStep);
        A.CallTo(() => _loginTicketRepository.RemoveEntityItem(_loginTicket))
            .MustHaveHappenedOnceExactly()
            .Then(
                A.CallTo(() =>
                        _signInUserCommandHandler.CreateItemAsync(
                            A<SignInUserCommand>.That.Matches(c => c.SecondFactorPassed && !c.EmailProven),
                            _user.Id,
                            A<CancellationToken>._
                        )
                    )
                    .MustHaveHappenedOnceExactly()
            )
            .Then(A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly());
        A.CallTo(() =>
                _recordFailedSignInCommandHandler.CreateItemAsync(A<RecordFailedSignInCommand>._, A<string>._, A<CancellationToken>._)
            )
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task A_Wrong_Authenticator_Code_Counts_Toward_Lockout_And_Gives_The_One_Message()
    {
        // Arrange
        _userDto.TwoFactorMethod = TwoFactorMethod.Authenticator;
        A.CallTo(() => _userManager.VerifyTwoFactorTokenAsync(A<IdentityUser>._, A<string>._, A<string>._)).Returns(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().CreateItemAsync(GetCommand(code: "000000"), string.Empty, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Codes.NotAccepted, exception.Message);
        A.CallTo(() =>
                _recordFailedSignInCommandHandler.CreateItemAsync(
                    new RecordFailedSignInCommand(_user, true),
                    _user.Id,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _signInUserCommandHandler.CreateItemAsync(A<SignInUserCommand>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task A_Right_Emailed_Code_Proves_The_Email()
    {
        // Arrange
        _userDto.TwoFactorMethod = TwoFactorMethod.Email;
        _loginTicket.CodeAction = UserMessages.LoginCodeAction;
        _loginTicket.CodeCorrelationId = _loginTicket.Id.ToString();
        A.CallTo(() =>
                _codeCheckService.CheckAsync(
                    _registeredApp.AppName,
                    UserMessages.LoginCodeAction,
                    _loginTicket.Id.ToString(),
                    "123456",
                    A<CancellationToken>._
                )
            )
            .Returns(CodeCheckOutcome.Verified);

        // Act
        await GetHandler().CreateItemAsync(GetCommand(code: "123456"), string.Empty, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() =>
                _signInUserCommandHandler.CreateItemAsync(
                    A<SignInUserCommand>.That.Matches(c => c.SecondFactorPassed && c.EmailProven),
                    _user.Id,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task A_Right_Texted_Code_Does_Not_Prove_The_Email()
    {
        // Arrange
        _userDto.TwoFactorMethod = TwoFactorMethod.Sms;
        _loginTicket.CodeAction = UserMessages.LoginCodeAction;
        _loginTicket.CodeCorrelationId = _loginTicket.Id.ToString();
        A.CallTo(() => _codeCheckService.CheckAsync(A<string>._, A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(CodeCheckOutcome.Verified);

        // Act
        await GetHandler().CreateItemAsync(GetCommand(code: "123456"), string.Empty, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() =>
                _signInUserCommandHandler.CreateItemAsync(
                    A<SignInUserCommand>.That.Matches(c => c.SecondFactorPassed && !c.EmailProven),
                    _user.Id,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(CodeCheckOutcome.WrongCode, true)]
    [InlineData(CodeCheckOutcome.Expired, false)]
    [InlineData(CodeCheckOutcome.NoAttemptsLeft, false)]
    [InlineData(CodeCheckOutcome.NotFound, false)]
    public async Task A_Failed_Emailed_Code_Counts_Toward_Lockout_Only_When_It_Is_Wrong(CodeCheckOutcome outcome, bool counts)
    {
        // Arrange
        _userDto.TwoFactorMethod = TwoFactorMethod.Email;
        _loginTicket.CodeAction = UserMessages.LoginCodeAction;
        _loginTicket.CodeCorrelationId = _loginTicket.Id.ToString();
        A.CallTo(() => _codeCheckService.CheckAsync(A<string>._, A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(outcome);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().CreateItemAsync(GetCommand(code: "123456"), string.Empty, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Codes.NotAccepted, exception.Message);
        A.CallTo(() =>
                _recordFailedSignInCommandHandler.CreateItemAsync(A<RecordFailedSignInCommand>._, A<string>._, A<CancellationToken>._)
            )
            .MustHaveHappened(counts ? 1 : 0, Times.Exactly);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_Recovery_Code_Works_Once_For_Any_Method(bool redeemed)
    {
        // Arrange
        _userDto.TwoFactorMethod = TwoFactorMethod.Sms;
        A.CallTo(() => _userManager.RedeemTwoFactorRecoveryCodeAsync(_user, "aaaa-1111"))
            .Returns(redeemed ? IdentityResult.Success : IdentityResult.Failed());

        // Act
        Task<AuthUserViewModel> verify() =>
            GetHandler().CreateItemAsync(GetCommand(recoveryCode: "aaaa-1111"), string.Empty, TestContext.Current.CancellationToken);

        // Assert
        if (redeemed)
            await verify();
        else
            await Assert.ThrowsAsync<InvalidDataException>((Func<Task<AuthUserViewModel>>)verify);

        A.CallTo(() => _codeCheckService.CheckAsync(A<string>._, A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() =>
                _recordFailedSignInCommandHandler.CreateItemAsync(A<RecordFailedSignInCommand>._, A<string>._, A<CancellationToken>._)
            )
            .MustHaveHappened(redeemed ? 0 : 1, Times.Exactly);
    }

    [Fact]
    public async Task A_Method_The_App_No_Longer_Allows_Gives_The_One_Message()
    {
        // Arrange
        _userDto.TwoFactorMethod = TwoFactorMethod.Sms;
        A.CallTo(() => _getSignInSettingsQueryHandler.GetItemAsync(A<GetSignInSettingsQuery>._, A<CancellationToken>._))
            .Returns(
                new SignInSettingsViewModel
                {
                    SignInSettings = new AppSignInSettingsDto { AppId = 1, AllowEmailTwoFactor = true },
                }
            );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().CreateItemAsync(GetCommand(code: "123456"), string.Empty, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Codes.NotAccepted, exception.Message);
    }

    private VerifySecondFactorCommand GetCommand(string? code = null, string? recoveryCode = null) =>
        new()
        {
            Ticket = "ticket",
            Code = code,
            RecoveryCode = recoveryCode,
            RegisteredApp = _registeredApp,
        };

    private VerifySecondFactorCommandHandler GetHandler() =>
        new(
            _userManager,
            _unitOfWork,
            _userRepository,
            _loginTicketRepository,
            _codeCheckService,
            _getSecondStepTicketQueryHandler,
            _getSignInSettingsQueryHandler,
            _recordFailedSignInCommandHandler,
            _signInUserCommandHandler
        );
}
