using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Commands;
using PolyzenKit.Application.Core.Messages.ViewModels;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Common.Settings;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Interfaces;
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

public class ConfirmEmailChangeCommandHandlerTests
{
    private static readonly DateTime _now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private readonly UserManager<IdentityUser> _userManager = IdentityFakes.UserManager();
    private readonly IUnitOfWork _unitOfWork = A.Fake<IUnitOfWork>();
    private readonly IUserRepository _userRepository = A.Fake<IUserRepository>();
    private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = A.Fake<IUserRegisteredAppRepository>();
    private readonly IEmailChangeRepository _emailChangeRepository = A.Fake<IEmailChangeRepository>();
    private readonly ICodeCheckService _codeCheckService = A.Fake<ICodeCheckService>();
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
    };

    private readonly EmailChangeEntity _emailChange = new()
    {
        Id = Guid.CreateVersion7(),
        UserId = "user-1",
        NewEmail = "new@test.com",
        Expires = _now.AddMinutes(5),
    };

    public ConfirmEmailChangeCommandHandlerTests()
    {
        A.CallTo(_userManager).WithReturnType<Task<IdentityResult>>().Returns(IdentityResult.Success);
        A.CallTo(() => _userManager.FindByIdAsync(_user.Id)).Returns(_user);
        A.CallTo(() => _userManager.GetRolesAsync(_user)).Returns(["testapp:user"]);
        A.CallTo(() => _userManager.FindByEmailAsync(A<string>._)).Returns((IdentityUser?)null);
        A.CallTo(() => _userManager.SetEmailAsync(_user, A<string?>._))
            .Invokes((IdentityUser user, string? email) => user.Email = email)
            .Returns(IdentityResult.Success);
        A.CallTo(() => _userManager.SetUserNameAsync(_user, A<string?>._))
            .Invokes((IdentityUser user, string? userName) => user.UserName = userName)
            .Returns(IdentityResult.Success);
        A.CallTo(() => _userRegisteredAppRepository.IsUserRegisteredForAppAsync(_user.Id, "TestApp", A<CancellationToken>._)).Returns(true);
        A.CallTo(() => _userRepository.GetUserDtoAsync(_user.Id, "TestApp", A<CancellationToken>._))
            .Returns(new UserDto { Id = _user.Id, FirstName = "Test" });
        A.CallTo(() =>
                _emailChangeRepository.GetEntityByAlternateIdAsync(
                    A<EmailChangeEntity>.That.Matches(e => e.UserId == _user.Id),
                    QueryCondition.MayOrMayNotExist,
                    A<CancellationToken>._
                )
            )
            .Returns(_emailChange);
        A.CallTo(() => _codeCheckService.CheckAsync(A<string>._, A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(CodeCheckOutcome.Verified);
    }

    [Fact]
    public async Task The_Right_Code_Sets_The_Confirmed_Email_With_The_User_Name_And_Notifies_The_Old_Address()
    {
        // Act
        await GetHandler().CreateItemAsync(GetCommand(), _user.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("new@test.com", _user.Email);
        Assert.True(_user.EmailConfirmed);
        Assert.Equal("new@test.com", _user.UserName);
        A.CallTo(() =>
                _codeCheckService.CheckAsync(
                    "TestApp",
                    UserMessages.LoginCodeAction,
                    _emailChange.Id.ToString(),
                    "123456",
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _emailChangeRepository.RemoveEntityItem(_emailChange)).MustHaveHappenedOnceExactly();
        A.CallTo(() =>
                _createOutboxMessageCommandHandler.CreateItemAsync(
                    A<CreateOutboxMessageCommand>.That.Matches(c =>
                        c.Name == UserMessages.ContactChanged && c.Message.Recipient!.Email == "old@test.com"
                    ),
                    A<string>._,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly());
    }

    [Theory]
    [InlineData(CodeCheckOutcome.WrongCode)]
    [InlineData(CodeCheckOutcome.Expired)]
    [InlineData(CodeCheckOutcome.NotFound)]
    public async Task A_Code_That_Fails_Changes_Nothing(CodeCheckOutcome outcome)
    {
        // Arrange
        A.CallTo(() => _codeCheckService.CheckAsync(A<string>._, A<string>._, A<string>._, A<string>._, A<CancellationToken>._))
            .Returns(outcome);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().CreateItemAsync(GetCommand(), _user.Id, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Codes.NotAccepted, exception.Message);
        Assert.Equal("old@test.com", _user.Email);
    }

    [Fact]
    public async Task An_Expired_Change_Still_Checks_A_Code_Then_Gives_The_Same_Message()
    {
        // Arrange
        _emailChange.Expires = _now;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().CreateItemAsync(GetCommand(), _user.Id, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Codes.NotAccepted, exception.Message);
        A.CallTo(() =>
                _codeCheckService.CheckAsync(
                    "TestApp",
                    UserMessages.LoginCodeAction,
                    A<string>.That.Not.IsEqualTo(_emailChange.Id.ToString()),
                    "123456",
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    private ConfirmEmailChangeCommand GetCommand() =>
        new()
        {
            UserId = _user.Id,
            RegisteredApp = _registeredApp,
            Changer = new UserChanger(_user.Id, false),
            Code = "123456",
        };

    private ConfirmEmailChangeCommandHandler GetHandler() =>
        new(
            Options.Create(new RoleSettings { ServiceName = "useridentity" }),
            _userManager,
            _unitOfWork,
            _userRepository,
            _userRegisteredAppRepository,
            _emailChangeRepository,
            _codeCheckService,
            _createOutboxMessageCommandHandler,
            new FakeTimeProvider(_now)
        );
}
