using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Commands;
using PolyzenKit.Application.Core.Messages.ViewModels;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Application.Interfaces;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.PasswordResets;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.LoginTickets;
using UserIdentity.Persistence.Repositories.PasswordResets;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using UserIdentity.Persistence.Repositories.Users;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class UpdatePasswordCommandHandlerTests
{
    private static readonly DateTime _now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private readonly UserManager<IdentityUser> _userManager = IdentityFakes.UserManager();
    private readonly ITokenFactory _tokenFactory = A.Fake<ITokenFactory>();
    private readonly IUnitOfWork _unitOfWork = A.Fake<IUnitOfWork>();
    private readonly IPasswordResetRepository _passwordResetRepository = A.Fake<IPasswordResetRepository>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = A.Fake<IRefreshTokenRepository>();
    private readonly ILoginTicketRepository _loginTicketRepository = A.Fake<ILoginTicketRepository>();
    private readonly IUserRepository _userRepository = A.Fake<IUserRepository>();
    private readonly ICodeCheckService _codeCheckService = A.Fake<ICodeCheckService>();
    private readonly ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> _createOutboxMessageCommandHandler =
        A.Fake<ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel>>();
    private readonly ICreateItemCommandHandler<SignInUserCommand, AuthUserViewModel> _signInUserCommandHandler = A.Fake<
        ICreateItemCommandHandler<SignInUserCommand, AuthUserViewModel>
    >();
    private readonly FakeTimeProvider _timeProvider = new(_now);

    private readonly ActiveRegisteredApp _registeredApp = new()
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
        AccessFailedCount = 3,
    };

    private readonly PasswordResetEntity _passwordReset;

    public UpdatePasswordCommandHandlerTests()
    {
        _passwordReset = new PasswordResetEntity
        {
            Id = Guid.NewGuid(),
            RequestId = "reset-hash",
            UserId = _user.Id,
            AppId = _registeredApp.Id,
            Expires = _now.AddMinutes(10),
        };

        A.CallTo(() => _tokenFactory.HashToken("reset")).Returns("reset-hash");
        A.CallTo(() =>
                _passwordResetRepository.GetEntityByAlternateIdAsync(
                    A<PasswordResetEntity>.That.Matches(e => e.RequestId == "reset-hash" && e.AppId == _registeredApp.Id),
                    QueryCondition.MayOrMayNotExist,
                    A<CancellationToken>._
                )
            )
            .Returns(_passwordReset);
        A.CallTo(() => _userManager.FindByIdAsync(_user.Id)).Returns(_user);
        A.CallTo(() => _userManager.GeneratePasswordResetTokenAsync(_user)).Returns("reset-token");
        A.CallTo(() => _userManager.ResetPasswordAsync(_user, "reset-token", "N3wP@ssword")).Returns(IdentityResult.Success);
        A.CallTo(() => _userRepository.GetUserDtoAsync(_user.Id, null, A<CancellationToken>._))
            .Returns(new UserDto { Id = _user.Id, FirstName = "Test" });
        A.CallTo(() => _codeCheckService.CheckAsync(A<string>._, A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(CodeCheckOutcome.Verified);
        A.CallTo(() => _signInUserCommandHandler.CreateItemAsync(A<SignInUserCommand>._, A<string>._, A<CancellationToken>._))
            .Returns(new AuthUserViewModel { User = new UserDto(), NextStep = SignInStep.Session });
    }

    [Fact]
    public async Task Update_Password_With_The_Right_Code_Applies_It_Ends_Sessions_Notifies_And_Signs_In_With_The_Email_Proved()
    {
        // Arrange
        var command = GetCommand();
        command.RequestContext = RequestContext.Create("203.0.113.7", "Test Browser");

        // Act
        var vm = await GetHandler().UpdateItemAsync(command, string.Empty, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SignInStep.Session, vm.NextStep);
        Assert.True(_passwordReset.Applied);
        Assert.Equal(0, _user.AccessFailedCount);
        A.CallTo(() =>
                _codeCheckService.CheckAsync(
                    _registeredApp.AppName,
                    UserMessages.PasswordResetAction,
                    _passwordReset.Id.ToString(),
                    "123456",
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _unitOfWork.BeginTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _userManager.ResetPasswordAsync(_user, "reset-token", "N3wP@ssword")).MustHaveHappenedOnceExactly())
            .Then(
                A.CallTo(() => _refreshTokenRepository.DeleteEntityItemsAsync(_user.Id, A<CancellationToken>._))
                    .MustHaveHappenedOnceExactly()
            )
            .Then(
                A.CallTo(() => _loginTicketRepository.DeleteEntityItemsAsync(_user.Id, A<CancellationToken>._))
                    .MustHaveHappenedOnceExactly()
            )
            .Then(
                A.CallTo(() =>
                        _createOutboxMessageCommandHandler.CreateItemAsync(
                            A<CreateOutboxMessageCommand>.That.Matches(c =>
                                c.Name == UserMessages.PasswordChanged
                                && c.Message.Action == UserMessages.PasswordChangedAction
                                && c.Message.Recipient!.Email == _user.Email
                                && c.Message.GetData<UserMessageData>()!.ChangedAt == _now
                                && c.Message.GetData<UserMessageData>()!.IpAddress == "203.0.113.7"
                            ),
                            _user.Id,
                            A<CancellationToken>._
                        )
                    )
                    .MustHaveHappenedOnceExactly()
            )
            .Then(
                A.CallTo(() =>
                        _signInUserCommandHandler.CreateItemAsync(
                            A<SignInUserCommand>.That.Matches(c => c.User == _user && c.EmailProven && !c.SecondFactorPassed),
                            _user.Id,
                            A<CancellationToken>._
                        )
                    )
                    .MustHaveHappenedOnceExactly()
            )
            .Then(A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly());
    }

    [Theory]
    [InlineData(CodeCheckOutcome.WrongCode)]
    [InlineData(CodeCheckOutcome.Expired)]
    [InlineData(CodeCheckOutcome.NoAttemptsLeft)]
    [InlineData(CodeCheckOutcome.NotFound)]
    public async Task Update_Password_With_A_Code_That_Fails_Changes_Nothing(CodeCheckOutcome outcome)
    {
        // Arrange
        A.CallTo(() => _codeCheckService.CheckAsync(A<string>._, A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(outcome);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().UpdateItemAsync(GetCommand(), string.Empty, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Codes.NotAccepted, exception.Message);
        Assert.False(_passwordReset.Applied);
        A.CallTo(() => _userManager.ResetPasswordAsync(A<IdentityUser>._, A<string>._, A<string>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData("applied")]
    [InlineData("expired")]
    public async Task Update_Password_For_A_Used_Or_Expired_Reset_Still_Checks_A_Code_Then_Refuses(string resetState)
    {
        // Arrange
        if (resetState == "applied")
            _passwordReset.Applied = true;
        else
            _passwordReset.Expires = _now;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().UpdateItemAsync(GetCommand(), string.Empty, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Codes.NotAccepted, exception.Message);
        A.CallTo(() =>
                _codeCheckService.CheckAsync(
                    _registeredApp.AppName,
                    UserMessages.PasswordResetAction,
                    A<string>.That.Not.IsEqualTo(_passwordReset.Id.ToString()),
                    "123456",
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _userManager.ResetPasswordAsync(A<IdentityUser>._, A<string>._, A<string>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Update_Password_With_A_Weak_Password_Throws_Before_Checking_The_Code()
    {
        // Arrange
        var passwordValidator = A.Fake<IPasswordValidator<IdentityUser>>();

        A.CallTo(() => passwordValidator.ValidateAsync(_userManager, A<IdentityUser>._, "N3wP@ssword"))
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Too weak." }));
        _userManager.PasswordValidators.Add(passwordValidator);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().UpdateItemAsync(GetCommand(), string.Empty, TestContext.Current.CancellationToken)
        );

        Assert.Equal("Too weak.", exception.Message);
        A.CallTo(() => _codeCheckService.CheckAsync(A<string>._, A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    private UpdatePasswordCommand GetCommand() =>
        new()
        {
            ResetRequestId = "reset",
            Code = "123456",
            NewPassword = "N3wP@ssword",
            RegisteredApp = _registeredApp,
        };

    private UpdatePasswordCommandHandler GetHandler() =>
        new(
            _userManager,
            _tokenFactory,
            _unitOfWork,
            _passwordResetRepository,
            _refreshTokenRepository,
            _loginTicketRepository,
            _userRepository,
            _codeCheckService,
            _createOutboxMessageCommandHandler,
            _signInUserCommandHandler,
            _timeProvider
        );
}
