using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Commands;
using PolyzenKit.Application.Core.Messages.Events;
using PolyzenKit.Application.Core.Messages.ViewModels;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Settings;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.StepUps;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.InviteCodes;
using UserIdentity.Persistence.Repositories.StepUps;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.Persistence.Repositories.Users;
using UserIdentity.Persistence.Repositories.WaitLists;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class DeleteUserCommandHandlerTests
{
    private const string GlobalAdminRole = "global:admin";

    private static readonly DateTime _now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    private readonly UserManager<IdentityUser> _userManager = IdentityFakes.UserManager();
    private readonly IUnitOfWork _unitOfWork = A.Fake<IUnitOfWork>();
    private readonly IUserRepository _userRepository = A.Fake<IUserRepository>();
    private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = A.Fake<IUserRegisteredAppRepository>();
    private readonly IInviteCodeRepository _inviteCodeRepository = A.Fake<IInviteCodeRepository>();
    private readonly IWaitListRepository _waitListRepository = A.Fake<IWaitListRepository>();
    private readonly IStepUpRepository _stepUpRepository = A.Fake<IStepUpRepository>();
    private readonly ITokenFactory _tokenFactory = A.Fake<ITokenFactory>();
    private readonly ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel> _recordFailedSignInCommandHandler = A.Fake<
        ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel>
    >();
    private readonly ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> _createOutboxMessageCommandHandler =
        A.Fake<ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel>>();

    private readonly RegisteredAppDto _registeredApp = new()
    {
        Id = 1,
        AppName = "TestApp",
        BaseUrl = "https://app.test",
    };

    private readonly IdentityUser _user = new()
    {
        Id = "user-1",
        UserName = "test.user",
        Email = "user@test.com",
        PhoneNumber = "0712345678",
    };

    private readonly UserEntity _userDetails = new()
    {
        Id = "user-1",
        FirstName = "Test",
        LastName = "User",
    };

    private readonly RequestContext _requestContext = RequestContext.Create("203.0.113.7", "Test Browser");

    private readonly StepUpEntity _stepUp = new()
    {
        Id = Guid.CreateVersion7(),
        UserId = "admin-id",
        AppId = 7,
        Ticket = "hashed-ticket",
        Expires = _now.AddMinutes(5),
    };

    private CreateOutboxMessageCommand? _outboxMessage;

    public DeleteUserCommandHandlerTests()
    {
        A.CallTo(_userManager).WithReturnType<Task<IdentityResult>>().Returns(IdentityResult.Success);
        A.CallTo(() => _userManager.FindByIdAsync(_user.Id)).Returns(_user);
        A.CallTo(() => _userManager.GetRolesAsync(_user)).Returns(["testapp:user"]);
        A.CallTo(() => _userManager.CheckPasswordAsync(_user, "right")).Returns(true);
        A.CallTo(() => _userManager.CheckPasswordAsync(_user, "wrong")).Returns(false);
        A.CallTo(() => _userManager.IsInRoleAsync(_user, GlobalAdminRole)).Returns(false);
        A.CallTo(() => _userRegisteredAppRepository.IsUserRegisteredForAppAsync(_user.Id, "TestApp", A<CancellationToken>._)).Returns(true);
        A.CallTo(() => _userRepository.GetEntityItemAsync(_user.Id, A<CancellationToken>._)).Returns(_userDetails);
        A.CallTo(() => _tokenFactory.HashToken("ticket")).Returns("hashed-ticket");
        A.CallTo(() =>
                _stepUpRepository.GetEntityByAlternateIdAsync(
                    A<StepUpEntity>.That.Matches(e => e.Ticket == "hashed-ticket" && e.AppId == 7),
                    QueryCondition.MayOrMayNotExist,
                    A<CancellationToken>._
                )
            )
            .Returns(_stepUp);
        A.CallTo(() =>
                _createOutboxMessageCommandHandler.CreateItemAsync(A<CreateOutboxMessageCommand>._, A<string>._, A<CancellationToken>._)
            )
            .Invokes((CreateOutboxMessageCommand command, string _, CancellationToken _) => _outboxMessage = command);
    }

    [Fact]
    public async Task Own_Deletion_Deletes_The_Invites_The_Wait_List_Entries_And_The_Account_In_One_Transaction()
    {
        // Act
        await GetHandler().DeleteItemAsync(GetOwnCommand("right"), _user.Id, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _unitOfWork.BeginTransactionAsync(A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(
                A.CallTo(() => _inviteCodeRepository.DeleteEntityItemsForUserAsync(_user.Id, A<CancellationToken>._))
                    .MustHaveHappenedOnceExactly()
            )
            .Then(
                A.CallTo(() => _waitListRepository.DeleteEntityItemsForUserAsync(_user.Id, A<CancellationToken>._))
                    .MustHaveHappenedOnceExactly()
            )
            .Then(
                A.CallTo(() =>
                        _createOutboxMessageCommandHandler.CreateItemAsync(
                            A<CreateOutboxMessageCommand>._,
                            _user.Id,
                            A<CancellationToken>._
                        )
                    )
                    .MustHaveHappenedOnceExactly()
            )
            .Then(A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _userManager.DeleteAsync(_user)).MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly());
        A.CallTo(() => _unitOfWork.RollbackTransactionAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Own_Deletion_Is_Announced_Correlated_By_The_User_Id_With_The_Request()
    {
        // Act
        await GetHandler().DeleteItemAsync(GetOwnCommand("right"), _user.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(_outboxMessage);
        Assert.Equal(UserMessages.UserDeleted, _outboxMessage.Name);
        Assert.Equal(UserMessages.AccountDeletedAction, _outboxMessage.Message.Action);
        Assert.Equal(_user.Id, _outboxMessage.Message.CorrelationId);
        Assert.Equal("TestApp", _outboxMessage.Message.RegisteredApp);
        Assert.Equal(
            new MessageRecipient
            {
                Email = "user@test.com",
                Phone = "0712345678",
                Name = "Test User",
            },
            _outboxMessage.Message.Recipient
        );
        Assert.Equal(
            new UserMessageData
            {
                FirstName = "Test",
                DeletedAt = _now,
                IpAddress = "203.0.113.7",
                Device = "Test Browser",
            },
            _outboxMessage.Message.GetData<UserMessageData>()
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Own_Deletion_Needs_The_Password(string? password)
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().DeleteItemAsync(GetOwnCommand(password), _user.Id, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Users.PasswordRequiredToDeleteAccount, exception.Message);
        A.CallTo(() => _userManager.CheckPasswordAsync(A<IdentityUser>._, A<string>._)).MustNotHaveHappened();
        AssertNothingDeleted();
    }

    [Fact]
    public async Task Own_Deletion_With_A_Wrong_Password_Records_A_Failed_Sign_In_And_Deletes_Nothing()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().DeleteItemAsync(GetOwnCommand("wrong"), _user.Id, TestContext.Current.CancellationToken)
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
        AssertNothingDeleted();
    }

    [Fact]
    public async Task A_Staff_Deletion_Needs_No_Password_And_Its_Notice_Carries_No_Request_Details()
    {
        // Act
        await GetHandler().DeleteItemAsync(GetStaffCommand(), "admin-id", TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _userManager.CheckPasswordAsync(A<IdentityUser>._, A<string>._)).MustNotHaveHappened();
        A.CallTo(() => _userManager.DeleteAsync(_user)).MustHaveHappenedOnceExactly();
        A.CallTo(() =>
                _createOutboxMessageCommandHandler.CreateItemAsync(A<CreateOutboxMessageCommand>._, "admin-id", A<CancellationToken>._)
            )
            .MustHaveHappenedOnceExactly();

        var data = _outboxMessage?.Message.GetData<UserMessageData>();

        Assert.Equal(_now, data?.DeletedAt);
        Assert.Null(data?.IpAddress);
        Assert.Null(data?.Device);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task A_Staff_Deletion_Without_A_Ticket_Asks_For_The_Sign_In_Code_First(string? ticket)
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<IllegalOperationException>(() =>
            GetHandler().DeleteItemAsync(GetStaffCommand(ticket), "admin-id", TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.TwoFactor.ConfirmFirst, exception.Message);
        A.CallTo(() => _stepUpRepository.GetEntityByAlternateIdAsync(A<StepUpEntity>._, A<QueryCondition>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        AssertNothingDeleted();
    }

    [Fact]
    public async Task A_Staff_Deletion_With_An_Unknown_Ticket_Is_Refused()
    {
        // Arrange
        A.CallTo(() => _tokenFactory.HashToken("unknown")).Returns("hashed-unknown");
        A.CallTo(() =>
                _stepUpRepository.GetEntityByAlternateIdAsync(
                    A<StepUpEntity>.That.Matches(e => e.Ticket == "hashed-unknown"),
                    A<QueryCondition>._,
                    A<CancellationToken>._
                )
            )
            .Returns((StepUpEntity?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<IllegalOperationException>(() =>
            GetHandler().DeleteItemAsync(GetStaffCommand("unknown"), "admin-id", TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.TwoFactor.ConfirmFirst, exception.Message);
        AssertNothingDeleted();
    }

    [Fact]
    public async Task A_Staff_Deletion_With_An_Expired_Ticket_Is_Refused()
    {
        // Arrange
        _stepUp.Expires = _now;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<IllegalOperationException>(() =>
            GetHandler().DeleteItemAsync(GetStaffCommand(), "admin-id", TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.TwoFactor.ConfirmFirst, exception.Message);
        AssertNothingDeleted();
    }

    [Fact]
    public async Task A_Staff_Deletion_With_Another_Admins_Ticket_Is_Refused()
    {
        // Arrange
        _stepUp.UserId = "other-admin-id";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<IllegalOperationException>(() =>
            GetHandler().DeleteItemAsync(GetStaffCommand(), "admin-id", TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.TwoFactor.ConfirmFirst, exception.Message);
        AssertNothingDeleted();
    }

    [Fact]
    public async Task A_Staff_Deletion_Looks_The_Ticket_Up_By_Its_Hash_In_The_Admins_Own_App()
    {
        // Act
        await GetHandler().DeleteItemAsync(GetStaffCommand(), "admin-id", TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() =>
                _stepUpRepository.GetEntityByAlternateIdAsync(
                    A<StepUpEntity>.That.Matches(e => e.Ticket == "hashed-ticket" && e.AppId == 7),
                    QueryCondition.MayOrMayNotExist,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _userManager.DeleteAsync(_user)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task A_Ticket_Works_Again_Until_It_Expires()
    {
        // Act
        await GetHandler().DeleteItemAsync(GetStaffCommand(), "admin-id", TestContext.Current.CancellationToken);
        await GetHandler().DeleteItemAsync(GetStaffCommand(), "admin-id", TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _userManager.DeleteAsync(_user)).MustHaveHappenedTwiceExactly();
        A.CallTo(() => _stepUpRepository.RemoveEntityItem(A<StepUpEntity>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Own_Deletion_Needs_No_Ticket()
    {
        // Act
        await GetHandler().DeleteItemAsync(GetOwnCommand("right"), _user.Id, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _stepUpRepository.GetEntityByAlternateIdAsync(A<StepUpEntity>._, A<QueryCondition>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => _userManager.DeleteAsync(_user)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task An_App_Admin_Cant_Delete_An_Admin_Account()
    {
        // Arrange
        A.CallTo(() => _userManager.GetRolesAsync(_user)).Returns(["testapp:admin"]);

        // Act & Assert
        await Assert.ThrowsAsync<NoRecordException>(() =>
            GetHandler().DeleteItemAsync(GetStaffCommand(), "admin-id", TestContext.Current.CancellationToken)
        );

        AssertNothingDeleted();
    }

    [Fact]
    public async Task The_Last_Global_Admin_Cant_Be_Deleted()
    {
        // Arrange
        A.CallTo(() => _userManager.IsInRoleAsync(_user, GlobalAdminRole)).Returns(true);
        A.CallTo(() => _userManager.GetUsersInRoleAsync(GlobalAdminRole)).Returns([_user]);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<IllegalOperationException>(() =>
            GetHandler().DeleteItemAsync(GetOwnCommand("right"), _user.Id, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Users.LastGlobalAdmin, exception.Message);
        AssertNothingDeleted();
    }

    [Fact]
    public async Task A_Global_Admin_Can_Be_Deleted_While_Another_Remains()
    {
        // Arrange
        A.CallTo(() => _userManager.IsInRoleAsync(_user, GlobalAdminRole)).Returns(true);
        A.CallTo(() => _userManager.GetUsersInRoleAsync(GlobalAdminRole)).Returns([_user, new IdentityUser { Id = "admin-2" }]);

        // Act
        await GetHandler().DeleteItemAsync(GetOwnCommand("right"), _user.Id, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _userManager.DeleteAsync(_user)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task A_Failed_Account_Delete_Rolls_Everything_Back()
    {
        // Arrange
        A.CallTo(() => _userManager.DeleteAsync(_user))
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Delete failed." }));

        // Act & Assert
        await Assert.ThrowsAsync<RecordUpdateException>(() =>
            GetHandler().DeleteItemAsync(GetOwnCommand("right"), _user.Id, TestContext.Current.CancellationToken)
        );

        A.CallTo(() => _unitOfWork.RollbackTransactionAsync(CancellationToken.None)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    private void AssertNothingDeleted()
    {
        A.CallTo(() => _unitOfWork.BeginTransactionAsync(A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _inviteCodeRepository.DeleteEntityItemsForUserAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _waitListRepository.DeleteEntityItemsForUserAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() =>
                _createOutboxMessageCommandHandler.CreateItemAsync(A<CreateOutboxMessageCommand>._, A<string>._, A<CancellationToken>._)
            )
            .MustNotHaveHappened();
        A.CallTo(() => _userManager.DeleteAsync(A<IdentityUser>._)).MustNotHaveHappened();
    }

    private DeleteUserCommand GetOwnCommand(string? password) =>
        new()
        {
            UserId = _user.Id,
            RegisteredApp = _registeredApp,
            Changer = new UserChanger(_user.Id, false),
            RequestContext = _requestContext,
            Password = password,
        };

    private DeleteUserCommand GetStaffCommand(string? ticket = "ticket") =>
        new()
        {
            UserId = _user.Id,
            RegisteredApp = _registeredApp,
            Changer = new UserChanger("admin-id", false),
            ChangerAppId = 7,
            RequestContext = _requestContext,
            Ticket = ticket,
        };

    private DeleteUserCommandHandler GetHandler() =>
        new(
            Options.Create(new RoleSettings { ServiceName = "useridentity" }),
            _userManager,
            _unitOfWork,
            _userRepository,
            _userRegisteredAppRepository,
            _inviteCodeRepository,
            _waitListRepository,
            _stepUpRepository,
            _tokenFactory,
            _recordFailedSignInCommandHandler,
            _createOutboxMessageCommandHandler,
            new FakeTimeProvider(_now)
        );
}
