using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Commands;
using PolyzenKit.Application.Core.Messages.ViewModels;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Settings;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.AccountLimits.Commands;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Application.Core.TwoFactor.Shared;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.EmailChanges;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.EmailChanges;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.Persistence.Repositories.Users;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class ChangeEmailCommandHandlerTests
{
    private static readonly DateTime _now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private readonly UserManager<IdentityUser> _userManager = IdentityFakes.UserManager();
    private readonly IUnitOfWork _unitOfWork = A.Fake<IUnitOfWork>();
    private readonly IUserRepository _userRepository = A.Fake<IUserRepository>();
    private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = A.Fake<IUserRegisteredAppRepository>();
    private readonly IEmailChangeRepository _emailChangeRepository = A.Fake<IEmailChangeRepository>();
    private readonly ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel> _recordFailedSignInCommandHandler = A.Fake<
        ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel>
    >();
    private readonly ICreateItemCommandHandler<
        CreateAccountLimitEventCommand,
        AccountLimitViewModel
    > _createAccountLimitEventCommandHandler = A.Fake<ICreateItemCommandHandler<CreateAccountLimitEventCommand, AccountLimitViewModel>>();
    private readonly ICreateItemCommandHandler<SendLoginCodeCommand, LoginCodeViewModel> _sendLoginCodeCommandHandler = A.Fake<
        ICreateItemCommandHandler<SendLoginCodeCommand, LoginCodeViewModel>
    >();
    private readonly ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> _createOutboxMessageCommandHandler =
        A.Fake<ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel>>();

    private readonly ActiveRegisteredApp _registeredApp = new()
    {
        Id = 1,
        AppName = "TestApp",
        BaseUrl = "https://app.test",
    };

    private readonly IdentityUser _user = new()
    {
        Id = "user-1",
        UserName = "old@test.com",
        Email = "old@test.com",
        EmailConfirmed = true,
    };

    private EmailChangeEntity? _emailChange;

    public ChangeEmailCommandHandlerTests()
    {
        A.CallTo(() => _userManager.FindByIdAsync(_user.Id)).Returns(_user);
        A.CallTo(() => _userManager.GetRolesAsync(_user)).Returns(["testapp:user"]);
        A.CallTo(() => _userManager.CheckPasswordAsync(_user, "right")).Returns(true);
        A.CallTo(() => _userManager.CheckPasswordAsync(_user, "wrong")).Returns(false);
        A.CallTo(() => _userManager.FindByEmailAsync(A<string>._)).Returns((IdentityUser?)null);
        A.CallTo(() => _userManager.FindByNameAsync(A<string>._)).Returns((IdentityUser?)null);
        A.CallTo(() => _userManager.SetEmailAsync(_user, A<string?>._))
            .Invokes(
                (IdentityUser user, string? email) =>
                {
                    user.Email = email;
                    user.EmailConfirmed = false;
                }
            )
            .Returns(IdentityResult.Success);
        A.CallTo(() => _userManager.SetUserNameAsync(_user, A<string?>._))
            .Invokes((IdentityUser user, string? userName) => user.UserName = userName)
            .Returns(IdentityResult.Success);
        A.CallTo(() => _userRegisteredAppRepository.IsUserRegisteredForAppAsync(_user.Id, "TestApp", A<CancellationToken>._)).Returns(true);
        A.CallTo(() => _userRepository.GetUserDtoAsync(_user.Id, "TestApp", A<CancellationToken>._))
            .Returns(new UserDto { Id = _user.Id, FirstName = "Test" });
        A.CallTo(() => _emailChangeRepository.CreateEntityItem(A<EmailChangeEntity>._))
            .Invokes((EmailChangeEntity entity) => _emailChange = entity);
        A.CallTo(() => _sendLoginCodeCommandHandler.CreateItemAsync(A<SendLoginCodeCommand>._, A<string>._, A<CancellationToken>._))
            .Returns(new LoginCodeViewModel { Sent = true, CodeSentTo = "n***@test.com" });
        A.CallTo(() =>
                _createAccountLimitEventCommandHandler.CreateItemAsync(
                    A<CreateAccountLimitEventCommand>._,
                    A<string>._,
                    A<CancellationToken>._
                )
            )
            .Returns(new AccountLimitViewModel { IsReached = false });
    }

    [Fact]
    public async Task The_Users_Own_Change_Stores_It_Pending_And_Sends_A_Code_To_The_New_Address()
    {
        // Act
        var vm = await GetHandler()
            .CreateItemAsync(GetOwnCommand("new@test.com", "right"), _user.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("n***@test.com", vm.CodeSentTo);
        Assert.Equal("old@test.com", _user.Email);
        Assert.NotNull(_emailChange);
        Assert.Equal("new@test.com", _emailChange.NewEmail);
        Assert.Equal(_now.AddSeconds(600), _emailChange.Expires);
        A.CallTo(() => _emailChangeRepository.DeleteEntityItemsAsync(_user.Id, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(
                A.CallTo(() =>
                        _sendLoginCodeCommandHandler.CreateItemAsync(
                            A<SendLoginCodeCommand>.That.Matches(c =>
                                c.Purpose == LoginCodePurpose.ChangeEmail
                                && c.Email == "new@test.com"
                                && c.PhoneNumber == null
                                && c.CorrelationId == _emailChange.Id.ToString()
                                && c.ExpiresAt == _emailChange.Expires
                            ),
                            _user.Id,
                            A<CancellationToken>._
                        )
                    )
                    .MustHaveHappenedOnceExactly()
            )
            .Then(A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly());
        A.CallTo(() => _userManager.SetEmailAsync(A<IdentityUser>._, A<string?>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task The_Users_Own_Change_To_A_Taken_Email_Answers_The_Same_And_Sends_No_Code()
    {
        // Arrange
        A.CallTo(() => _userManager.FindByEmailAsync("taken@test.com")).Returns(new IdentityUser { Id = "other-user" });

        // Act
        var vm = await GetHandler()
            .CreateItemAsync(GetOwnCommand("taken@test.com", "right"), _user.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("t***@test.com", vm.CodeSentTo);
        A.CallTo(() => _sendLoginCodeCommandHandler.CreateItemAsync(A<SendLoginCodeCommand>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() =>
                _createAccountLimitEventCommandHandler.CreateItemAsync(
                    new CreateAccountLimitEventCommand(_user.Id, AccountLimitKind.LoginCodeSent),
                    A<string>._,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task The_Users_Own_Change_Past_The_Login_Code_Cap_Is_Refused()
    {
        // Arrange
        A.CallTo(() => _sendLoginCodeCommandHandler.CreateItemAsync(A<SendLoginCodeCommand>._, A<string>._, A<CancellationToken>._))
            .Returns(new LoginCodeViewModel { Sent = false });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<IllegalOperationException>(() =>
            GetHandler().CreateItemAsync(GetOwnCommand("new@test.com", "right"), _user.Id, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Users.TooManyCodes, exception.Message);
        A.CallTo(() => _unitOfWork.RollbackTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(null, ErrorMessages.Users.PasswordRequired)]
    [InlineData("wrong", ErrorMessages.Users.WrongPassword)]
    public async Task The_Users_Own_Change_Needs_The_Right_Password(string? password, string message)
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().CreateItemAsync(GetOwnCommand("new@test.com", password), _user.Id, TestContext.Current.CancellationToken)
        );

        Assert.Equal(message, exception.Message);
        A.CallTo(() => _sendLoginCodeCommandHandler.CreateItemAsync(A<SendLoginCodeCommand>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task An_Admins_Change_Applies_At_Once_Unconfirmed_With_The_User_Name_And_Notifies_The_Old_Address()
    {
        // Act
        await GetHandler().CreateItemAsync(GetAdminCommand("new@test.com"), "admin-id", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("new@test.com", _user.Email);
        Assert.False(_user.EmailConfirmed);
        Assert.Equal("new@test.com", _user.UserName);
        A.CallTo(() =>
                _createOutboxMessageCommandHandler.CreateItemAsync(
                    A<CreateOutboxMessageCommand>.That.Matches(c =>
                        c.Name == UserMessages.ContactChanged
                        && c.Message.Recipient!.Email == "old@test.com"
                        && c.Message.GetData<UserMessageData>()!.Change == nameof(ContactChange.Email)
                        && c.Message.GetData<UserMessageData>()!.IpAddress == null
                    ),
                    "admin-id",
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _sendLoginCodeCommandHandler.CreateItemAsync(A<SendLoginCodeCommand>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => _userManager.CheckPasswordAsync(A<IdentityUser>._, A<string>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task An_Admins_Change_Keeps_A_User_Name_That_Was_Not_The_Email()
    {
        // Arrange
        _user.UserName = "test.user";

        // Act
        await GetHandler().CreateItemAsync(GetAdminCommand("new@test.com"), "admin-id", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("test.user", _user.UserName);
        A.CallTo(() => _userManager.SetUserNameAsync(A<IdentityUser>._, A<string?>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task An_Admins_Change_To_A_Taken_Email_Gets_The_Generic_Error()
    {
        // Arrange
        A.CallTo(() => _userManager.FindByEmailAsync("taken@test.com")).Returns(new IdentityUser { Id = "other-user" });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().CreateItemAsync(GetAdminCommand("taken@test.com"), "admin-id", TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Users.EmailNotChangedByAdmin, exception.Message);
        A.CallTo(() => _userManager.SetEmailAsync(A<IdentityUser>._, A<string?>._)).MustNotHaveHappened();
    }

    private ChangeEmailCommand GetOwnCommand(string email, string? password) =>
        new()
        {
            UserId = _user.Id,
            RegisteredApp = _registeredApp,
            Changer = new UserChanger(_user.Id, false),
            Email = email,
            Password = password,
        };

    private ChangeEmailCommand GetAdminCommand(string email) =>
        new()
        {
            UserId = _user.Id,
            RegisteredApp = _registeredApp,
            Changer = new UserChanger("admin-id", false),
            Email = email,
        };

    private ChangeEmailCommandHandler GetHandler() =>
        new(
            Options.Create(new RoleSettings { ServiceName = "useridentity" }),
            Options.Create(new TwoFactorSettings()),
            _userManager,
            _unitOfWork,
            _userRepository,
            _userRegisteredAppRepository,
            _emailChangeRepository,
            _recordFailedSignInCommandHandler,
            _createAccountLimitEventCommandHandler,
            _sendLoginCodeCommandHandler,
            _createOutboxMessageCommandHandler,
            new FakeTimeProvider(_now)
        );
}
