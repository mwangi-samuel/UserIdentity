using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Commands;
using PolyzenKit.Application.Core.Messages.ViewModels;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Application.Core.SignInSettings.Queries;
using UserIdentity.Application.Core.SignInSettings.ViewModels;
using UserIdentity.Application.Core.TwoFactor.Commands;
using UserIdentity.Application.Core.TwoFactor.Queries;
using UserIdentity.Application.Core.TwoFactor.Shared;
using UserIdentity.Application.Core.TwoFactor.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Application.Interfaces;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Common.Identity;
using UserIdentity.Domain.AppSignInSettings;
using UserIdentity.Domain.LoginTickets;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.LoginTickets;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using UserIdentity.Persistence.Repositories.Users;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.TwoFactor.Commands;

public class ConfirmTwoFactorSetupCommandHandlerTests
{
    private static readonly DateTime _now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private readonly UserManager<IdentityUser> _userManager = IdentityFakes.UserManager();
    private readonly IUnitOfWork _unitOfWork = A.Fake<IUnitOfWork>();
    private readonly IUserRepository _userRepository = A.Fake<IUserRepository>();
    private readonly ILoginTicketRepository _loginTicketRepository = A.Fake<ILoginTicketRepository>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = A.Fake<IRefreshTokenRepository>();
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
    private readonly ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> _createOutboxMessageCommandHandler =
        A.Fake<ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel>>();
    private readonly ICreateItemCommandHandler<SignInUserCommand, AuthUserViewModel> _signInUserCommandHandler = A.Fake<
        ICreateItemCommandHandler<SignInUserCommand, AuthUserViewModel>
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
    };

    private readonly UserEntity _userDetails = new() { Id = "user-1", FirstName = "Test" };

    private readonly LoginTicketEntity _loginTicket = new()
    {
        Id = Guid.CreateVersion7(),
        UserId = "user-1",
        Step = SignInStep.SetUpSecondFactor,
        Expires = _now.AddMinutes(10),
        EmailProven = true,
    };

    private readonly string[] _recoveryCodes = ["aaaa-1111", "bbbb-2222"];

    public ConfirmTwoFactorSetupCommandHandlerTests()
    {
        A.CallTo(_userManager).WithReturnType<Task<IdentityResult>>().Returns(IdentityResult.Success);
        A.CallTo(() => _userManager.GenerateNewTwoFactorRecoveryCodesAsync(_user, 10)).Returns(_recoveryCodes);
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
        A.CallTo(() => _userRepository.GetEntityItemAsync(_user.Id, A<CancellationToken>._)).Returns(_userDetails);
        A.CallTo(() => _signInUserCommandHandler.CreateItemAsync(A<SignInUserCommand>._, A<string>._, A<CancellationToken>._))
            .Returns(new AuthUserViewModel { User = new UserDto(), NextStep = SignInStep.Session });
    }

    [Fact]
    public async Task Confirming_Text_Messages_Stores_The_Confirmed_Phone_The_Method_And_New_Recovery_Codes()
    {
        // Arrange
        _loginTicket.Method = TwoFactorMethod.Sms;
        _loginTicket.PhoneNumber = "+254700000123";
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
        var vm = await GetHandler().CreateItemAsync(GetCommand("123456"), string.Empty, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SignInStep.Session, vm.NextStep);
        Assert.Equal(_recoveryCodes, vm.RecoveryCodes);
        Assert.Equal("+254700000123", _user.PhoneNumber);
        Assert.True(_user.PhoneNumberConfirmed);
        Assert.Equal(TwoFactorMethod.Sms, _userDetails.TwoFactorMethod);
        A.CallTo(() => _userManager.SetTwoFactorEnabledAsync(_user, true)).MustHaveHappenedOnceExactly();
        A.CallTo(() =>
                _userManager.RemoveAuthenticationTokenAsync(_user, UserTokenNames.InternalLoginProvider, UserTokenNames.AuthenticatorKey)
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() =>
                _createOutboxMessageCommandHandler.CreateItemAsync(
                    A<CreateOutboxMessageCommand>.That.Matches(c =>
                        c.Name == UserMessages.TwoFactorChanged
                        && c.Message.GetData<UserMessageData>()!.Change == nameof(TwoFactorChange.SetUp)
                        && c.Message.GetData<UserMessageData>()!.Method == TwoFactorMethod.Sms
                    ),
                    _user.Id,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _refreshTokenRepository.DeleteEntityItemsAsync(_user.Id, null, null, null, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _loginTicketRepository.RemoveEntityItem(_loginTicket)).MustHaveHappenedOnceExactly())
            .Then(
                A.CallTo(() =>
                        _signInUserCommandHandler.CreateItemAsync(
                            A<SignInUserCommand>.That.Matches(c => c.SecondFactorPassed && c.EmailProven),
                            _user.Id,
                            A<CancellationToken>._
                        )
                    )
                    .MustHaveHappenedOnceExactly()
            )
            .Then(A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task Confirming_An_Authenticator_Keeps_Its_Key_And_Reports_A_Change_Of_Method()
    {
        // Arrange
        _loginTicket.Method = TwoFactorMethod.Authenticator;
        _userDetails.TwoFactorMethod = TwoFactorMethod.Email;
        A.CallTo(() => _userManager.VerifyTwoFactorTokenAsync(_user, TokenOptions.DefaultAuthenticatorProvider, "123456")).Returns(true);

        // Act
        await GetHandler().CreateItemAsync(GetCommand("123456"), string.Empty, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TwoFactorMethod.Authenticator, _userDetails.TwoFactorMethod);
        A.CallTo(() => _userManager.RemoveAuthenticationTokenAsync(A<IdentityUser>._, A<string>._, A<string>._)).MustNotHaveHappened();
        A.CallTo(() =>
                _createOutboxMessageCommandHandler.CreateItemAsync(
                    A<CreateOutboxMessageCommand>.That.Matches(c =>
                        c.Message.GetData<UserMessageData>()!.Change == nameof(TwoFactorChange.Changed)
                    ),
                    A<string>._,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task A_Wrong_Authenticator_Code_Counts_Toward_Lockout_And_Saves_Nothing()
    {
        // Arrange
        _loginTicket.Method = TwoFactorMethod.Authenticator;
        A.CallTo(() => _userManager.VerifyTwoFactorTokenAsync(A<IdentityUser>._, A<string>._, A<string>._)).Returns(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().CreateItemAsync(GetCommand("000000"), string.Empty, TestContext.Current.CancellationToken)
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
        A.CallTo(() => _userManager.SetTwoFactorEnabledAsync(A<IdentityUser>._, A<bool>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Confirming_Email_Needs_No_Code_And_Removes_Any_Authenticator_Key()
    {
        // Arrange
        _loginTicket.Method = TwoFactorMethod.Email;

        // Act
        var vm = await GetHandler().CreateItemAsync(GetCommand(null), string.Empty, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_recoveryCodes, vm.RecoveryCodes);
        Assert.Equal(TwoFactorMethod.Email, _userDetails.TwoFactorMethod);
        A.CallTo(() =>
                _userManager.RemoveAuthenticationTokenAsync(_user, UserTokenNames.InternalLoginProvider, UserTokenNames.AuthenticatorKey)
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _codeCheckService.CheckAsync(A<string>._, A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Confirming_Before_Choosing_A_Method_Is_Refused()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().CreateItemAsync(GetCommand("123456"), string.Empty, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.TwoFactor.ChooseMethodFirst, exception.Message);
    }

    [Fact]
    public async Task Confirming_An_Authenticator_Without_A_Code_Is_Refused()
    {
        // Arrange
        _loginTicket.Method = TwoFactorMethod.Authenticator;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().CreateItemAsync(GetCommand(null), string.Empty, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.TwoFactor.CodeRequired, exception.Message);
    }

    private ConfirmTwoFactorSetupCommand GetCommand(string? code) =>
        new()
        {
            Ticket = "ticket",
            Code = code,
            RegisteredApp = _registeredApp,
        };

    private ConfirmTwoFactorSetupCommandHandler GetHandler() =>
        new(
            Options.Create(new TwoFactorSettings()),
            _userManager,
            _unitOfWork,
            _userRepository,
            _loginTicketRepository,
            _refreshTokenRepository,
            _codeCheckService,
            _getSecondStepTicketQueryHandler,
            _getSignInSettingsQueryHandler,
            _recordFailedSignInCommandHandler,
            _createOutboxMessageCommandHandler,
            _signInUserCommandHandler,
            new FakeTimeProvider(_now)
        );
}
