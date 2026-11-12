using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Commands;
using PolyzenKit.Application.Core.Messages.Events;
using PolyzenKit.Application.Core.Messages.ViewModels;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Settings;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Common.Enums;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.InviteCodes;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.Persistence.Repositories.Users;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class RegisterUserCommandHandlerTests
{
    private readonly IOptions<RoleSettings> _roleSettings;
    private readonly IOptions<IdentityOptions> _identityOptions;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserRepository _userRepository;
    private readonly IUserRegisteredAppRepository _userRegisteredAppRepository;
    private readonly IInviteCodeRepository _inviteCodeRepository;
    private static readonly DateTime _now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private readonly FakeTimeProvider _timeProvider = new();
    private readonly ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> _createOutboxMessageCommandHandler;
    private readonly ICreateItemCommandHandler<StartSessionCommand, UserTokenViewModel> _startSessionCommandHandler;
    private readonly ICreateItemCommandHandler<CreateLoginTicketCommand, LoginTicketViewModel> _createLoginTicketCommandHandler;

    public RegisterUserCommandHandlerTests()
    {
        _roleSettings = A.Fake<IOptions<RoleSettings>>();
        _identityOptions = A.Fake<IOptions<IdentityOptions>>();
        _userManager = IdentityFakes.UserManager();
        _roleManager = A.Fake<RoleManager<IdentityRole>>();
        _unitOfWork = A.Fake<IUnitOfWork>();
        _userRepository = A.Fake<IUserRepository>();
        _userRegisteredAppRepository = A.Fake<IUserRegisteredAppRepository>();
        _inviteCodeRepository = A.Fake<IInviteCodeRepository>();
        _timeProvider.SetUtcNow(_now);
        _createOutboxMessageCommandHandler = A.Fake<ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel>>();
        _startSessionCommandHandler = A.Fake<ICreateItemCommandHandler<StartSessionCommand, UserTokenViewModel>>();
        _createLoginTicketCommandHandler = A.Fake<ICreateItemCommandHandler<CreateLoginTicketCommand, LoginTicketViewModel>>();

        A.CallTo(() => _unitOfWork.BeginTransactionAsync(A<CancellationToken>._)).Returns(Task.CompletedTask);
        A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).Returns(Task.CompletedTask);
        A.CallTo(() => _unitOfWork.RollbackTransactionAsync(A<CancellationToken>._)).Returns(Task.CompletedTask);
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Create_User_When_Default_Role_Not_Created_Throws_RecordCreationException()
    {
        var command = GetRegisterUserCommand();
        var defaultRole = GetDefaultRoleName();

        A.CallTo(() => _roleManager.FindByNameAsync(defaultRole)).Returns((IdentityRole?)null);
        A.CallTo(() => _roleManager.CreateAsync(A<IdentityRole>.That.Matches(r => r.Name == defaultRole))).Returns(IdentityResult.Failed());

        var handler = GetRegisterUserCommandHandler();

        await Assert.ThrowsAsync<RecordCreationException>(() =>
            handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );
        A.CallTo(() => _roleManager.FindByNameAsync(defaultRole)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _roleManager.CreateAsync(A<IdentityRole>.That.Matches(r => r.Name == defaultRole))).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Create_User_When_UserName_Exists_Throws_RecordExistsException()
    {
        var command = GetRegisterUserCommand();
        var defaultRole = new IdentityRole(GetDefaultRoleName());

        A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).Returns(defaultRole);
        A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(new IdentityUser());

        var handler = GetRegisterUserCommandHandler();

        await Assert.ThrowsAsync<RecordExistsException>(() =>
            handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Create_User_When_Email_Exists_Throws_RecordExistsException()
    {
        var command = GetRegisterUserCommand();
        var defaultRole = new IdentityRole(GetDefaultRoleName());

        A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).Returns(defaultRole);
        A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns((IdentityUser?)null);
        A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).Returns(new IdentityUser());

        var handler = GetRegisterUserCommandHandler();

        await Assert.ThrowsAsync<RecordExistsException>(() =>
            handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Create_User_When_Identity_User_Creation_Fails_Throws_InvalidDataException()
    {
        var command = GetRegisterUserCommand();
        var defaultRole = new IdentityRole(GetDefaultRoleName());

        A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).Returns(defaultRole);
        A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns((IdentityUser?)null);
        A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).Returns((IdentityUser?)null);
        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, command.UserPassword)).Returns(IdentityResult.Failed());

        var handler = GetRegisterUserCommandHandler();

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, command.UserPassword)).MustHaveHappenedOnceExactly();
        A.CallTo(() =>
                _createOutboxMessageCommandHandler.CreateItemAsync(A<CreateOutboxMessageCommand>._, A<string>._, A<CancellationToken>._)
            )
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Create_User_When_Assigning_Default_Role_Fails_Throws_RecordCreationException()
    {
        var command = GetRegisterUserCommand();
        var defaultRole = new IdentityRole(GetDefaultRoleName());

        A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).Returns(defaultRole);
        A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns((IdentityUser?)null);
        A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).Returns((IdentityUser?)null);
        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, command.UserPassword)).Returns(IdentityResult.Success);
        A.CallTo(() => _userManager.AddToRolesAsync(A<IdentityUser>._, A<IEnumerable<string>>._)).Returns(IdentityResult.Failed());

        var handler = GetRegisterUserCommandHandler();

        await Assert.ThrowsAsync<RecordCreationException>(() =>
            handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, command.UserPassword)).MustHaveHappenedOnceExactly();
        string[] expectedRoles = [defaultRole.Name!];
        A.CallTo(() => _userManager.AddToRolesAsync(A<IdentityUser>._, A<IEnumerable<string>>.That.IsSameSequenceAs(expectedRoles)))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Create_User_With_Valid_Details_Returns_AuthUser()
    {
        var command = GetRegisterUserCommand();
        var defaultRole = new IdentityRole(GetDefaultRoleName());
        var session = new UserTokenViewModel
        {
            UserToken = new UserTokenDto
            {
                AccessToken = new AccessTokenDto { Token = "access-token", ExpiresIn = 1200 },
                RefreshToken = "refresh-token",
            },
        };

        string? userId = null;
        IdentityUser? createdUser = null;

        A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).Returns(defaultRole);
        A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns((IdentityUser?)null);
        A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).Returns((IdentityUser?)null);
        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, command.UserPassword))
            .Invokes(
                (IdentityUser u, string _) =>
                {
                    createdUser = u;
                    userId = u.Id;
                }
            )
            .Returns(IdentityResult.Success);
        A.CallTo(() => _userManager.AddToRolesAsync(A<IdentityUser>._, A<IEnumerable<string>>._)).Returns(IdentityResult.Success);
        A.CallTo(() => _userRepository.CreateEntityItem(A<UserEntity>.That.Matches(u => u.FirstName == command.FirstName))).DoesNothing();
        A.CallTo(() =>
                _startSessionCommandHandler.CreateItemAsync(
                    A<StartSessionCommand>.That.Matches(c =>
                        c.UserId == userId && c.UserName == command.UserName && c.AppName == command.RegisteredApp.AppName
                    ),
                    A<string>._,
                    A<CancellationToken>._
                )
            )
            .Returns(session);
        A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).DoesNothing();

        var handler = GetRegisterUserCommandHandler();

        var vm = await handler.CreateItemAsync(command, userId!, TestContext.Current.CancellationToken);

        Assert.Equal(userId, vm.User.Id);
        Assert.Equal(command.FirstName, vm.User.FirstName);
        Assert.Equal(command.LastName, vm.User.LastName);
        Assert.Equal(command.UserEmail, vm.User.Email);
        Assert.Equal(_now, vm.User.CreatedAt);
        Assert.Equal(_now, vm.User.UpdatedAt);
        Assert.Same(session.UserToken, vm.UserToken);
        Assert.Null(vm.LoginTicket);

        A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, command.UserPassword)).MustHaveHappenedOnceExactly();
        string[] expectedRoles = [defaultRole.Name!];
        A.CallTo(() => _userManager.AddToRolesAsync(A<IdentityUser>._, A<IEnumerable<string>>.That.IsSameSequenceAs(expectedRoles)))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _userRepository.CreateEntityItem(A<UserEntity>.That.Matches(u => u.FirstName == command.FirstName)))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _createLoginTicketCommandHandler.CreateItemAsync(A<CreateLoginTicketCommand>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(RequestSource.UI, true)]
    [InlineData(RequestSource.Api, false)]
    public async Task Create_Unconfirmed_User_Gets_A_Login_Ticket_Only_From_The_Apps_UI(RequestSource requestSource, bool getsTicket)
    {
        // Arrange
        var command = GetRegisterUserCommand();
        command.RequestSource = requestSource;
        SetUpSuccessfulSignUp(command);

        var identityOptions = new IdentityOptions();
        identityOptions.SignIn.RequireConfirmedAccount = true;

        A.CallTo(() =>
                _createLoginTicketCommandHandler.CreateItemAsync(A<CreateLoginTicketCommand>._, A<string>._!, A<CancellationToken>._)
            )
            .Returns(new LoginTicketViewModel { Ticket = "login-ticket" });

        var handler = GetRegisterUserCommandHandler(Options.Create(identityOptions));

        // Act
        var vm = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(vm.IsConfirmed);
        Assert.Null(vm.UserToken);
        Assert.Equal(getsTicket ? "login-ticket" : null, vm.LoginTicket);
        A.CallTo(() =>
                _createLoginTicketCommandHandler.CreateItemAsync(
                    A<CreateLoginTicketCommand>.That.Matches(c => c.UserId == vm.User.Id && c.AppId == command.RegisteredApp.Id),
                    A<string>._!,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappened(getsTicket ? 1 : 0, Times.Exactly);
        A.CallTo(() => _startSessionCommandHandler.CreateItemAsync(A<StartSessionCommand>._, A<string>._!, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Create_User_Adds_A_Welcome_Message_Before_Committing()
    {
        // Arrange
        var command = GetRegisterUserCommand();
        var defaultRole = new IdentityRole(GetDefaultRoleName());
        string? userId = null;

        A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).Returns(defaultRole);
        A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns((IdentityUser?)null);
        A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).Returns((IdentityUser?)null);
        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, command.UserPassword))
            .Invokes((IdentityUser u, string _) => userId = u.Id)
            .Returns(IdentityResult.Success);
        A.CallTo(() => _userManager.AddToRolesAsync(A<IdentityUser>._, A<IEnumerable<string>>._)).Returns(IdentityResult.Success);

        var handler = GetRegisterUserCommandHandler();

        // Act
        await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        var recipient = new MessageRecipient
        {
            Email = command.UserEmail,
            Phone = command.PhoneNumber,
            Name = $"{command.FirstName} {command.LastName}",
        };
        var data = new UserMessageData
        {
            UserId = userId,
            UserName = command.UserName,
            FirstName = command.FirstName,
            LastName = command.LastName,
        };

        A.CallTo(() =>
                _createOutboxMessageCommandHandler.CreateItemAsync(
                    A<CreateOutboxMessageCommand>.That.Matches(c =>
                        c.Name == UserMessages.UserRegistered
                        && c.Message.Action == UserMessages.WelcomeUserAction
                        && c.Message.CorrelationId == userId
                        && c.Message.RegisteredApp == command.RegisteredApp.AppName
                        && c.Message.Recipient == recipient
                        && c.Message.GetData<UserMessageData>() == data
                    ),
                    A<string>._,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly());
        A.CallTo(() =>
                _createOutboxMessageCommandHandler.CreateItemAsync(A<CreateOutboxMessageCommand>._, A<string>._, A<CancellationToken>._)
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Create_User_For_A_Service_With_A_Callback_Url_Adds_A_Callback_Message()
    {
        // Arrange
        var command = GetRegisterUserCommand();
        command.RequestSource = RequestSource.Api;
        command.RegisteredApp = GetRegisteredApp(callbackUrl: "https://callback.test");
        SetUpSuccessfulSignUp(command);

        var handler = GetRegisterUserCommandHandler();

        // Act
        var vm = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() =>
                _createOutboxMessageCommandHandler.CreateItemAsync(
                    A<CreateOutboxMessageCommand>.That.Matches(c =>
                        c.Name == UserMessages.CallbackRequested
                        && c.Message.Action == UserMessages.WelcomeUserAction
                        && c.Message.CorrelationId == vm.User.Id
                        && c.Message.RegisteredApp == command.RegisteredApp.AppName
                    ),
                    A<string>._!,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly());
    }

    [Theory]
    [InlineData(RequestSource.UI, "https://callback.test")]
    [InlineData(RequestSource.Api, null)]
    public async Task Create_User_From_The_Apps_UI_Or_For_An_App_Without_A_Callback_Url_Adds_No_Callback_Message(
        RequestSource requestSource,
        string? callbackUrl
    )
    {
        // Arrange
        var command = GetRegisterUserCommand();
        command.RequestSource = requestSource;
        command.RegisteredApp = GetRegisteredApp(callbackUrl: callbackUrl);
        SetUpSuccessfulSignUp(command);

        var handler = GetRegisterUserCommandHandler();

        // Act
        await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() =>
                _createOutboxMessageCommandHandler.CreateItemAsync(
                    A<CreateOutboxMessageCommand>.That.Matches(c => c.Name == UserMessages.UserRegistered),
                    A<string>._!,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() =>
                _createOutboxMessageCommandHandler.CreateItemAsync(
                    A<CreateOutboxMessageCommand>.That.Matches(c => c.Name == UserMessages.CallbackRequested),
                    A<string>._!,
                    A<CancellationToken>._
                )
            )
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Create_User_With_Invite_Code_Marks_Invite_Code_Applied_By_New_User()
    {
        var command = GetRegisterUserCommand();
        command.RegisteredApp = GetRegisteredApp(requireInviteCode: true);
        const string inviteCode = "INVITE01";
        command = command with { InviteCode = inviteCode };

        var defaultRole = new IdentityRole(GetDefaultRoleName());
        var inviteCodeEntity = new InviteCodeEntity
        {
            Id = 7,
            InviteCode = inviteCode,
            UserEmail = command.UserEmail!,
            Applied = false,
        };
        string? userId = null;

        A.CallTo(() =>
                _inviteCodeRepository.GetEntityByAlternateIdAsync(
                    A<InviteCodeEntity>.That.Matches(e => e.UserEmail == command.UserEmail),
                    QueryCondition.MayOrMayNotExist,
                    A<CancellationToken>._
                )
            )
            .Returns(inviteCodeEntity);
        A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).Returns(defaultRole);
        A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns((IdentityUser?)null);
        A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).Returns((IdentityUser?)null);
        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, command.UserPassword))
            .Invokes((IdentityUser u, string _) => userId = u.Id)
            .Returns(IdentityResult.Success);
        A.CallTo(() => _userManager.AddToRolesAsync(A<IdentityUser>._, A<IEnumerable<string>>._)).Returns(IdentityResult.Success);

        var handler = GetRegisterUserCommandHandler();

        await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

        Assert.True(inviteCodeEntity.Applied);
        Assert.NotNull(userId);
        Assert.Equal(userId, inviteCodeEntity.UpdatedBy);
        Assert.Equal(_now, inviteCodeEntity.UpdatedAt);

        A.CallTo(() => _inviteCodeRepository.UpdateEntityItem(inviteCodeEntity)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Create_User_With_Applied_Invite_Code_Throws_InvalidDataException()
    {
        var command = GetRegisterUserCommand();
        command.RegisteredApp = GetRegisteredApp(requireInviteCode: true);
        const string inviteCode = "INVITE01";
        command = command with { InviteCode = inviteCode };

        var inviteCodeEntity = new InviteCodeEntity
        {
            Id = 7,
            InviteCode = inviteCode,
            UserEmail = command.UserEmail!,
            Applied = true,
        };

        A.CallTo(() =>
                _inviteCodeRepository.GetEntityByAlternateIdAsync(
                    A<InviteCodeEntity>._,
                    QueryCondition.MayOrMayNotExist,
                    A<CancellationToken>._
                )
            )
            .Returns(inviteCodeEntity);

        var handler = GetRegisterUserCommandHandler();

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        A.CallTo(() => _inviteCodeRepository.UpdateEntityItem(A<InviteCodeEntity>._)).MustNotHaveHappened();
        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, A<string>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Create_User_With_Optional_Invite_Code_Grants_The_Invites_Role()
    {
        // Arrange
        var command = GetRegisterUserCommand();
        const string inviteCode = "INVITE01";
        const string inviteRole = "testapp:owner";
        command = command with { InviteCode = inviteCode.ToLowerInvariant() };

        var defaultRole = new IdentityRole(GetDefaultRoleName());
        var inviteCodeEntity = new InviteCodeEntity
        {
            Id = 7,
            InviteCode = inviteCode,
            UserEmail = command.UserEmail!,
            Applied = false,
            Role = inviteRole,
        };

        A.CallTo(() =>
                _inviteCodeRepository.GetEntityByAlternateIdAsync(
                    A<InviteCodeEntity>.That.Matches(e => e.UserEmail == command.UserEmail && e.AppId == command.RegisteredApp.Id),
                    QueryCondition.MayOrMayNotExist,
                    A<CancellationToken>._
                )
            )
            .Returns(inviteCodeEntity);
        A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).Returns(defaultRole);
        A.CallTo(() => _roleManager.FindByNameAsync(inviteRole)).Returns(new IdentityRole(inviteRole));
        A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns((IdentityUser?)null);
        A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).Returns((IdentityUser?)null);
        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, command.UserPassword)).Returns(IdentityResult.Success);
        A.CallTo(() => _userManager.AddToRolesAsync(A<IdentityUser>._, A<IEnumerable<string>>._)).Returns(IdentityResult.Success);

        var handler = GetRegisterUserCommandHandler();

        // Act
        var vm = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(command.UserEmail, vm.User.Email);
        Assert.True(inviteCodeEntity.Applied);

        string[] expectedRoles = [defaultRole.Name!, inviteRole];
        A.CallTo(() => _userManager.AddToRolesAsync(A<IdentityUser>._, A<IEnumerable<string>>.That.IsSameSequenceAs(expectedRoles)))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _inviteCodeRepository.UpdateEntityItem(inviteCodeEntity)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Create_User_With_Invalid_Optional_Invite_Code_Throws_InvalidDataException()
    {
        // Arrange
        var command = GetRegisterUserCommand();
        command = command with { InviteCode = "WRONGCODE" };

        var inviteCodeEntity = new InviteCodeEntity
        {
            Id = 7,
            InviteCode = "INVITE01",
            UserEmail = command.UserEmail!,
            Applied = false,
            Role = "testapp:owner",
        };

        A.CallTo(() =>
                _inviteCodeRepository.GetEntityByAlternateIdAsync(
                    A<InviteCodeEntity>._,
                    QueryCondition.MayOrMayNotExist,
                    A<CancellationToken>._
                )
            )
            .Returns(inviteCodeEntity);

        var handler = GetRegisterUserCommandHandler();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, A<string>._)).MustNotHaveHappened();
        A.CallTo(() => _inviteCodeRepository.UpdateEntityItem(A<InviteCodeEntity>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong")]
    [InlineData("used")]
    public async Task Create_User_With_A_Missing_Wrong_Or_Used_Invite_Code_Gives_The_Same_Error(string inviteState)
    {
        // Arrange
        var command = GetRegisterUserCommand();
        command = command with { InviteCode = "ABCDEFGHJK" };

        var inviteCodeEntity = inviteState switch
        {
            "missing" => null,
            "wrong" => new InviteCodeEntity
            {
                InviteCode = "KJHGFEDCBA",
                UserEmail = command.UserEmail!,
                Applied = false,
            },
            _ => new InviteCodeEntity
            {
                InviteCode = "ABCDEFGHJK",
                UserEmail = command.UserEmail!,
                Applied = true,
            },
        };

        A.CallTo(() =>
                _inviteCodeRepository.GetEntityByAlternateIdAsync(
                    A<InviteCodeEntity>._,
                    QueryCondition.MayOrMayNotExist,
                    A<CancellationToken>._
                )
            )
            .Returns(inviteCodeEntity);

        var handler = GetRegisterUserCommandHandler();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        Assert.Equal("A valid invite code is required when signing up.", exception.Message);
        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, A<string>._)).MustNotHaveHappened();
        A.CallTo(() => _unitOfWork.RollbackTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Create_User_When_The_Invites_Role_Is_Missing_Throws_InvalidDataException_And_Rolls_Back()
    {
        // Arrange
        var command = GetRegisterUserCommand();
        const string inviteRole = "testapp:owner";
        command = command with { InviteCode = "INVITE01" };

        var defaultRole = new IdentityRole(GetDefaultRoleName());
        var inviteCodeEntity = new InviteCodeEntity
        {
            Id = 7,
            InviteCode = "INVITE01",
            UserEmail = command.UserEmail!,
            Applied = false,
            Role = inviteRole,
        };

        A.CallTo(() =>
                _inviteCodeRepository.GetEntityByAlternateIdAsync(
                    A<InviteCodeEntity>._,
                    QueryCondition.MayOrMayNotExist,
                    A<CancellationToken>._
                )
            )
            .Returns(inviteCodeEntity);
        A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).Returns(defaultRole);
        A.CallTo(() => _roleManager.FindByNameAsync(inviteRole)).Returns((IdentityRole?)null);
        A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns((IdentityUser?)null);
        A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).Returns((IdentityUser?)null);
        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, command.UserPassword)).Returns(IdentityResult.Success);
        A.CallTo(() => _userManager.AddToRolesAsync(A<IdentityUser>._, A<IEnumerable<string>>._)).Returns(IdentityResult.Success);

        var handler = GetRegisterUserCommandHandler();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        A.CallTo(() => _userManager.AddToRolesAsync(A<IdentityUser>._, A<IEnumerable<string>>._)).MustNotHaveHappened();
        A.CallTo(() => _unitOfWork.RollbackTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Create_User_Without_Invite_Code_When_Not_Required_Does_Not_Check_Invites(string? inviteCode)
    {
        // Arrange
        var command = GetRegisterUserCommand();
        command = command with { InviteCode = inviteCode };

        var defaultRole = new IdentityRole(GetDefaultRoleName());

        A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).Returns(defaultRole);
        A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns((IdentityUser?)null);
        A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).Returns((IdentityUser?)null);
        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, command.UserPassword)).Returns(IdentityResult.Success);
        A.CallTo(() => _userManager.AddToRolesAsync(A<IdentityUser>._, A<IEnumerable<string>>._)).Returns(IdentityResult.Success);

        var handler = GetRegisterUserCommandHandler();

        // Act
        var vm = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(command.UserName, vm.User.UserName);

        A.CallTo(() =>
                _inviteCodeRepository.GetEntityByAlternateIdAsync(A<InviteCodeEntity>._, A<QueryCondition>._, A<CancellationToken>._)
            )
            .MustNotHaveHappened();
        A.CallTo(() => _inviteCodeRepository.UpdateEntityItem(A<InviteCodeEntity>._)).MustNotHaveHappened();
        string[] expectedRoles = [defaultRole.Name!];
        A.CallTo(() => _userManager.AddToRolesAsync(A<IdentityUser>._, A<IEnumerable<string>>.That.IsSameSequenceAs(expectedRoles)))
            .MustHaveHappenedOnceExactly();
    }

    private RegisterUserCommandHandler GetRegisterUserCommandHandler(IOptions<IdentityOptions>? identityOptions = null)
    {
        return new RegisterUserCommandHandler(
            _roleSettings,
            identityOptions ?? _identityOptions,
            _userManager,
            _roleManager,
            _unitOfWork,
            _userRepository,
            _userRegisteredAppRepository,
            _inviteCodeRepository,
            _timeProvider,
            _createOutboxMessageCommandHandler,
            _startSessionCommandHandler,
            _createLoginTicketCommandHandler
        );
    }

    private void SetUpSuccessfulSignUp(RegisterUserCommand command)
    {
        var defaultRole = new IdentityRole(GetDefaultRoleName());

        A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).Returns(defaultRole);
        A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns((IdentityUser?)null);
        A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).Returns((IdentityUser?)null);
        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, command.UserPassword)).Returns(IdentityResult.Success);
        A.CallTo(() => _userManager.AddToRolesAsync(A<IdentityUser>._, A<IEnumerable<string>>._)).Returns(IdentityResult.Success);
    }

    private string GetDefaultRoleName()
    {
        var settings = _roleSettings.Value;
        var command = GetRegisterUserCommand();
        return $"{command.RegisteredApp.AppName.Trim().ToLowerInvariant()}{Separators.ServiceRole}{settings.DefaultRole.Trim().ToLowerInvariant()}";
    }

    private static RegisterUserCommand GetRegisterUserCommand()
    {
        return new RegisterUserCommand
        {
            FirstName = "Test",
            LastName = "User",
            UserName = "test.user",
            PhoneNumber = "0712345678",
            UserEmail = "test.user@domain.com",
            UserPassword = "Password123",
            RegisteredApp = GetRegisteredApp(),
            RequestSource = RequestSource.Api,
        };
    }

    private static ActiveRegisteredApp GetRegisteredApp(string? callbackUrl = null, bool requireInviteCode = false) =>
        new()
        {
            Id = 1,
            AppName = "TestApp",
            AppSecretKey = "test-secret",
            BaseUrl = "https://app.test",
            CallbackUrl = callbackUrl,
            RequireInviteCode = requireInviteCode,
        };
}
