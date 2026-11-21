using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Commands;
using PolyzenKit.Application.Core.Messages.ViewModels;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Application.Core.TwoFactor.Commands;
using UserIdentity.Application.Core.TwoFactor.Queries;
using UserIdentity.Application.Core.TwoFactor.Shared;
using UserIdentity.Application.Core.TwoFactor.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Common.Identity;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.Users;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.TwoFactor.Commands;

public class ProfileTwoFactorCommandHandlerTests
{
    private static readonly DateTime _now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private readonly UserManager<IdentityUser> _userManager = IdentityFakes.UserManager();
    private readonly IUnitOfWork _unitOfWork = A.Fake<IUnitOfWork>();
    private readonly IUserRepository _userRepository = A.Fake<IUserRepository>();
    private readonly IGetItemQueryHandler<GetTwoFactorStatusQuery, TwoFactorStatusViewModel> _getTwoFactorStatusQueryHandler = A.Fake<
        IGetItemQueryHandler<GetTwoFactorStatusQuery, TwoFactorStatusViewModel>
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

    private readonly IdentityUser _user = new() { Id = "user-1", Email = "test@example.com" };
    private readonly UserEntity _userDetails = new() { Id = "user-1", FirstName = "Test" };

    public ProfileTwoFactorCommandHandlerTests()
    {
        A.CallTo(_userManager).WithReturnType<Task<IdentityResult>>().Returns(IdentityResult.Success);
        A.CallTo(() => _userManager.FindByIdAsync(_user.Id)).Returns(_user);
        A.CallTo(() => _userManager.CheckPasswordAsync(_user, "right")).Returns(true);
        A.CallTo(() => _userManager.CheckPasswordAsync(_user, "wrong")).Returns(false);
        A.CallTo(() => _userRepository.GetEntityItemAsync(_user.Id, A<CancellationToken>._)).Returns(_userDetails);
        A.CallTo(() => _signInUserCommandHandler.CreateItemAsync(A<SignInUserCommand>._, A<string>._, A<CancellationToken>._))
            .Returns(new AuthUserViewModel { User = new UserDto(), NextStep = SignInStep.ConfirmEmail });
        SetUpStatus(method: TwoFactorMethod.Authenticator, required: false);
    }

    [Fact]
    public async Task Change_With_The_Right_Password_Asks_For_The_Email_Before_Setting_Up()
    {
        // Act
        var vm = await new ChangeTwoFactorCommandHandler(
            _userManager,
            _unitOfWork,
            _recordFailedSignInCommandHandler,
            _signInUserCommandHandler
        ).CreateItemAsync(
            new ChangeTwoFactorCommand
            {
                UserId = _user.Id,
                Password = "right",
                RegisteredApp = _registeredApp,
            },
            _user.Id,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(SignInStep.ConfirmEmail, vm.NextStep);
        A.CallTo(() =>
                _signInUserCommandHandler.CreateItemAsync(
                    A<SignInUserCommand>.That.Matches(c => c.ChangingMethod && !c.EmailProven),
                    _user.Id,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Change_With_A_Wrong_Password_Counts_Toward_Lockout()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            new ChangeTwoFactorCommandHandler(
                _userManager,
                _unitOfWork,
                _recordFailedSignInCommandHandler,
                _signInUserCommandHandler
            ).CreateItemAsync(
                new ChangeTwoFactorCommand
                {
                    UserId = _user.Id,
                    Password = "wrong",
                    RegisteredApp = _registeredApp,
                },
                _user.Id,
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(ErrorMessages.Users.WrongPassword, exception.Message);
        A.CallTo(() =>
                _recordFailedSignInCommandHandler.CreateItemAsync(
                    new RecordFailedSignInCommand(_user, false),
                    _user.Id,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _signInUserCommandHandler.CreateItemAsync(A<SignInUserCommand>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task A_Locked_Out_User_Gets_The_Lockout_Message_Without_A_Password_Check()
    {
        // Arrange
        A.CallTo(() => _userManager.IsLockedOutAsync(_user)).Returns(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetReplaceRecoveryCodesHandler().CreateItemAsync(GetReplaceCommand("right"), _user.Id, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Users.AccountLocked, exception.Message);
        A.CallTo(() => _userManager.CheckPasswordAsync(A<IdentityUser>._, A<string>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task New_Recovery_Codes_Replace_The_Old_Ones()
    {
        // Arrange
        A.CallTo(() => _userManager.GenerateNewTwoFactorRecoveryCodesAsync(_user, 10)).Returns(["cccc-3333"]);

        // Act
        var vm = await GetReplaceRecoveryCodesHandler()
            .CreateItemAsync(GetReplaceCommand("right"), _user.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["cccc-3333"], vm.RecoveryCodes);
    }

    [Fact]
    public async Task New_Recovery_Codes_Need_A_Method()
    {
        // Arrange
        SetUpStatus(method: null, required: false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<IllegalOperationException>(() =>
            GetReplaceRecoveryCodesHandler().CreateItemAsync(GetReplaceCommand("right"), _user.Id, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.TwoFactor.NotOn, exception.Message);
    }

    [Fact]
    public async Task Turning_Off_Removes_The_Method_Key_And_Codes_And_Notifies()
    {
        // Arrange
        _userDetails.TwoFactorMethod = TwoFactorMethod.Authenticator;

        // Act
        var vm = await GetDisableHandler().UpdateItemAsync(GetDisableCommand(), _user.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(vm.Method);
        Assert.Null(_userDetails.TwoFactorMethod);
        A.CallTo(() =>
                _userManager.RemoveAuthenticationTokenAsync(_user, UserTokenNames.InternalLoginProvider, UserTokenNames.AuthenticatorKey)
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() =>
                _userManager.RemoveAuthenticationTokenAsync(_user, UserTokenNames.InternalLoginProvider, UserTokenNames.RecoveryCodes)
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _userManager.SetTwoFactorEnabledAsync(_user, false)).MustHaveHappenedOnceExactly();
        A.CallTo(() =>
                _createOutboxMessageCommandHandler.CreateItemAsync(
                    A<CreateOutboxMessageCommand>.That.Matches(c =>
                        c.Name == UserMessages.TwoFactorChanged
                        && c.Message.GetData<UserMessageData>()!.Change == nameof(TwoFactorChange.TurnedOff)
                    ),
                    _user.Id,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task Turning_Off_While_It_Is_Required_Is_Refused()
    {
        // Arrange
        SetUpStatus(method: TwoFactorMethod.Authenticator, required: true);
        _userDetails.TwoFactorMethod = TwoFactorMethod.Authenticator;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<IllegalOperationException>(() =>
            GetDisableHandler().UpdateItemAsync(GetDisableCommand(), _user.Id, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.TwoFactor.Required, exception.Message);
        A.CallTo(() => _userManager.SetTwoFactorEnabledAsync(A<IdentityUser>._, A<bool>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Turning_Off_When_It_Is_Off_Is_Refused()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<IllegalOperationException>(() =>
            GetDisableHandler().UpdateItemAsync(GetDisableCommand(), _user.Id, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.TwoFactor.NotOn, exception.Message);
    }

    private void SetUpStatus(TwoFactorMethod? method, bool required) =>
        A.CallTo(() => _getTwoFactorStatusQueryHandler.GetItemAsync(A<GetTwoFactorStatusQuery>._, A<CancellationToken>._))
            .Returns(
                new TwoFactorStatusViewModel
                {
                    Method = method,
                    Required = required,
                    RecoveryCodesLeft = method is null ? 0 : 10,
                    AllowedMethods = [TwoFactorMethod.Authenticator],
                }
            );

    private ReplaceRecoveryCodesCommand GetReplaceCommand(string password) =>
        new()
        {
            UserId = _user.Id,
            Password = password,
            RegisteredApp = _registeredApp,
        };

    private DisableTwoFactorCommand GetDisableCommand() =>
        new()
        {
            UserId = _user.Id,
            Password = "right",
            RegisteredApp = _registeredApp,
        };

    private ReplaceRecoveryCodesCommandHandler GetReplaceRecoveryCodesHandler() =>
        new(Options.Create(new TwoFactorSettings()), _userManager, _getTwoFactorStatusQueryHandler, _recordFailedSignInCommandHandler);

    private DisableTwoFactorCommandHandler GetDisableHandler() =>
        new(
            _userManager,
            _unitOfWork,
            _userRepository,
            _getTwoFactorStatusQueryHandler,
            _recordFailedSignInCommandHandler,
            _createOutboxMessageCommandHandler,
            new FakeTimeProvider(_now)
        );
}
