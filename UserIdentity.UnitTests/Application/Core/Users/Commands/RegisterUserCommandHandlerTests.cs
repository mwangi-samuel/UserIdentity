using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Time.Testing;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.Messages.Commands;
using PolyzenKit.Application.Core.Messages.ViewModels;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.AccountLimits.Commands;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Application.Core.SignInSettings.Queries;
using UserIdentity.Application.Core.SignInSettings.ViewModels;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Common.Enums;
using UserIdentity.Common.Errors;
using UserIdentity.Domain.AppSignInSettings;
using UserIdentity.Domain.InviteCodes;
using UserIdentity.Domain.UserRegisteredApps;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.InviteCodes;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.Persistence.Repositories.Users;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class RegisterUserCommandHandlerTests
{
    private const string DefaultRoleName = "testapp:user";

    private static readonly DateTime _now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    private readonly UserManager<IdentityUser> _userManager = IdentityFakes.UserManager();
    private readonly RoleManager<IdentityRole> _roleManager = A.Fake<RoleManager<IdentityRole>>();
    private readonly IUnitOfWork _unitOfWork = A.Fake<IUnitOfWork>();
    private readonly IUserRepository _userRepository = A.Fake<IUserRepository>();
    private readonly IUserRegisteredAppRepository _userRegisteredAppRepository = A.Fake<IUserRegisteredAppRepository>();
    private readonly IInviteCodeRepository _inviteCodeRepository = A.Fake<IInviteCodeRepository>();
    private readonly ITokenFactory _tokenFactory = A.Fake<ITokenFactory>();
    private readonly IPasswordHasher<IdentityUser> _passwordHasher = A.Fake<IPasswordHasher<IdentityUser>>();
    private readonly FakeTimeProvider _timeProvider = new(_now);

    private readonly IGetItemQueryHandler<GetSignInSettingsQuery, SignInSettingsViewModel> _getSignInSettingsQueryHandler = A.Fake<
        IGetItemQueryHandler<GetSignInSettingsQuery, SignInSettingsViewModel>
    >();
    private readonly ICreateItemCommandHandler<
        CreateAccountLimitEventCommand,
        AccountLimitViewModel
    > _createAccountLimitEventCommandHandler = A.Fake<ICreateItemCommandHandler<CreateAccountLimitEventCommand, AccountLimitViewModel>>();
    private readonly ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel> _createOutboxMessageCommandHandler =
        A.Fake<ICreateItemCommandHandler<CreateOutboxMessageCommand, OutboxMessageViewModel>>();
    private readonly ICreateItemCommandHandler<CreateLoginTicketCommand, LoginTicketViewModel> _createLoginTicketCommandHandler = A.Fake<
        ICreateItemCommandHandler<CreateLoginTicketCommand, LoginTicketViewModel>
    >();
    private readonly ICreateItemCommandHandler<SignInUserCommand, AuthUserViewModel> _signInUserCommandHandler = A.Fake<
        ICreateItemCommandHandler<SignInUserCommand, AuthUserViewModel>
    >();

    private readonly RegisteredAppDto _registeredApp = new()
    {
        Id = 1,
        AppName = "TestApp",
        BaseUrl = "https://app.test",
    };

    private AppSignInSettingsDto _signInSettings = new() { AppId = 1, DefaultRole = "user" };

    public RegisterUserCommandHandlerTests()
    {
        _userManager.PasswordHasher = _passwordHasher;

        A.CallTo(() => _getSignInSettingsQueryHandler.GetItemAsync(A<GetSignInSettingsQuery>._, A<CancellationToken>._))
            .ReturnsLazily(() => new SignInSettingsViewModel { SignInSettings = _signInSettings });
        A.CallTo(() =>
                _createAccountLimitEventCommandHandler.CreateItemAsync(
                    A<CreateAccountLimitEventCommand>._,
                    A<string>._,
                    A<CancellationToken>._
                )
            )
            .Returns(new AccountLimitViewModel { IsReached = false });
        A.CallTo(() => _createLoginTicketCommandHandler.CreateItemAsync(A<CreateLoginTicketCommand>._, A<string>._, A<CancellationToken>._))
            .Returns(new LoginTicketViewModel { Ticket = "login-ticket", Expires = _now.AddHours(1) });
        A.CallTo(() => _tokenFactory.GenerateToken(A<int>._)).Returns("unsaved-ticket");
    }

    [Fact]
    public async Task Sign_Up_When_The_Default_Role_Cannot_Be_Created_Throws_RecordCreationException()
    {
        // Arrange
        var command = GetRegisterUserCommand();

        SetUpSuccessfulSignUp(command);
        A.CallTo(() => _roleManager.FindByNameAsync(DefaultRoleName)).Returns((IdentityRole?)null);
        A.CallTo(() => _roleManager.CreateAsync(A<IdentityRole>.That.Matches(r => r.Name == DefaultRoleName)))
            .Returns(IdentityResult.Failed());

        // Act & Assert
        await Assert.ThrowsAsync<RecordCreationException>(() =>
            GetHandler().CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        A.CallTo(() => _unitOfWork.RollbackTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Sign_Up_With_A_Taken_Name_Or_Email_When_Confirmation_Is_Required_Answers_Like_A_New_Sign_Up(
        bool userNameTaken,
        bool emailTaken
    )
    {
        // Arrange
        _signInSettings = new AppSignInSettingsDto { AppId = 1, RequireConfirmedAccount = true };
        var command = GetRegisterUserCommand();
        command.RequestContext = RequestContext.Create("203.0.113.7", "Test Browser");
        var existingUser = new IdentityUser
        {
            Id = "existing-user",
            UserName = "existing.user",
            Email = "existing@domain.com",
        };

        SetUpSuccessfulSignUp(command);
        A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(userNameTaken ? existingUser : null);
        A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).Returns(emailTaken ? existingUser : null);
        A.CallTo(() => _userRepository.GetUserDtoAsync(existingUser.Id, null, A<CancellationToken>._))
            .Returns(new UserDto { Id = existingUser.Id, FirstName = "Existing" });

        // Act
        var vm = await GetHandler().CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SignInStep.ConfirmEmail, vm.NextStep);
        Assert.Equal("unsaved-ticket", vm.Ticket);
        Assert.Equal("t***@domain.com", vm.CodeSentTo);
        Assert.Null(vm.UserToken);
        Assert.NotEqual(existingUser.Id, vm.User.Id);
        Assert.Equal(command.UserName, vm.User.UserName);
        Assert.Equal(_now, vm.User.CreatedAt);
        A.CallTo(() => _passwordHasher.HashPassword(A<IdentityUser>._, command.UserPassword)).MustHaveHappenedOnceExactly();
        A.CallTo(() =>
                _createOutboxMessageCommandHandler.CreateItemAsync(
                    A<CreateOutboxMessageCommand>.That.Matches(c =>
                        c.Name == UserMessages.SignUpAttempted
                        && c.Message.Action == UserMessages.SignUpAttemptedAction
                        && c.Message.Recipient!.Email == existingUser.Email
                        && c.Message.GetData<UserMessageData>()!.FirstName == "Existing"
                        && c.Message.GetData<UserMessageData>()!.AttemptedAt == _now
                        && c.Message.GetData<UserMessageData>()!.IpAddress == "203.0.113.7"
                        && c.Message.GetData<UserMessageData>()!.Device == "Test Browser"
                    ),
                    existingUser.Id,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._)).MustNotHaveHappened();
        A.CallTo(() => _createLoginTicketCommandHandler.CreateItemAsync(A<CreateLoginTicketCommand>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Sign_Up_With_A_Taken_Email_Past_The_Notice_Cap_Sends_No_Notice_And_Answers_The_Same()
    {
        // Arrange
        _signInSettings = new AppSignInSettingsDto { AppId = 1, RequireConfirmedAccount = true };
        var command = GetRegisterUserCommand();
        var existingUser = new IdentityUser { Id = "existing-user", Email = command.UserEmail };

        SetUpSuccessfulSignUp(command);
        A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).Returns(existingUser);
        A.CallTo(() =>
                _createAccountLimitEventCommandHandler.CreateItemAsync(
                    new CreateAccountLimitEventCommand(existingUser.Id, AccountLimitKind.SignUpAttemptNotice),
                    A<string>._,
                    A<CancellationToken>._
                )
            )
            .Returns(new AccountLimitViewModel { IsReached = true });

        // Act
        var vm = await GetHandler().CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SignInStep.ConfirmEmail, vm.NextStep);
        Assert.Equal("unsaved-ticket", vm.Ticket);
        A.CallTo(() =>
                _createOutboxMessageCommandHandler.CreateItemAsync(A<CreateOutboxMessageCommand>._, A<string>._, A<CancellationToken>._)
            )
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Sign_Up_With_A_Taken_Email_When_Confirmation_Is_Not_Required_Throws_The_Generic_Error()
    {
        // Arrange
        var command = GetRegisterUserCommand();

        SetUpSuccessfulSignUp(command);
        A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).Returns(new IdentityUser { Id = "existing-user" });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Users.AccountNotCreated, exception.Message);
        A.CallTo(() => _passwordHasher.HashPassword(A<IdentityUser>._, command.UserPassword)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._)).MustNotHaveHappened();
        A.CallTo(() =>
                _createOutboxMessageCommandHandler.CreateItemAsync(A<CreateOutboxMessageCommand>._, A<string>._, A<CancellationToken>._)
            )
            .MustNotHaveHappened();
        A.CallTo(() => _unitOfWork.RollbackTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Sign_Up_With_A_Weak_Password_Throws_Before_Looking_For_Existing_Accounts()
    {
        // Arrange
        var command = GetRegisterUserCommand();
        var passwordValidator = A.Fake<IPasswordValidator<IdentityUser>>();

        SetUpSuccessfulSignUp(command);
        A.CallTo(() => passwordValidator.ValidateAsync(_userManager, A<IdentityUser>._, command.UserPassword))
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Passwords must have at least one digit." }));
        _userManager.PasswordValidators.Add(passwordValidator);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        Assert.Equal("Passwords must have at least one digit.", exception.Message);
        A.CallTo(() => _userManager.FindByNameAsync(A<string>._)).MustNotHaveHappened();
        A.CallTo(() => _userManager.FindByEmailAsync(A<string>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Sign_Up_When_Identity_Refuses_The_User_Throws_InvalidDataException()
    {
        // Arrange
        var command = GetRegisterUserCommand();

        SetUpSuccessfulSignUp(command);
        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._)).Returns(IdentityResult.Failed());

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        A.CallTo(() => _userRepository.CreateEntityItem(A<UserEntity>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Sign_Up_When_Granting_The_Default_Role_Fails_Throws_RecordCreationException()
    {
        // Arrange
        var command = GetRegisterUserCommand();

        SetUpSuccessfulSignUp(command);
        A.CallTo(() => _userManager.AddToRolesAsync(A<IdentityUser>._, A<IEnumerable<string>>._)).Returns(IdentityResult.Failed());

        // Act & Assert
        await Assert.ThrowsAsync<RecordCreationException>(() =>
            GetHandler().CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        string[] expectedRoles = [DefaultRoleName];
        A.CallTo(() => _userManager.AddToRolesAsync(A<IdentityUser>._, A<IEnumerable<string>>.That.IsSameSequenceAs(expectedRoles)))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Sign_Up_When_Confirmation_Is_Not_Required_Continues_Through_Sign_In()
    {
        // Arrange
        var command = GetRegisterUserCommand();
        var signedIn = new AuthUserViewModel { User = new UserDto(), NextStep = SignInStep.Session };

        SetUpSuccessfulSignUp(command);
        A.CallTo(() => _signInUserCommandHandler.CreateItemAsync(A<SignInUserCommand>._, A<string>._, A<CancellationToken>._))
            .Returns(signedIn);

        // Act
        var vm = await GetHandler().CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(signedIn, vm);
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._))
            .MustHaveHappenedOnceExactly()
            .Then(
                A.CallTo(() =>
                        _signInUserCommandHandler.CreateItemAsync(
                            A<SignInUserCommand>.That.Matches(c =>
                                c.User.UserName == command.UserName
                                && c.RegisteredApp == _registeredApp
                                && !c.EmailProven
                                && !c.SecondFactorPassed
                            ),
                            A<string>._,
                            A<CancellationToken>._
                        )
                    )
                    .MustHaveHappenedOnceExactly()
            )
            .Then(A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly());
        A.CallTo(() =>
                _createOutboxMessageCommandHandler.CreateItemAsync(A<CreateOutboxMessageCommand>._, A<string>._, A<CancellationToken>._)
            )
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Sign_Up_When_Confirmation_Is_Required_Sends_The_Welcome_Code_And_Answers_ConfirmEmail()
    {
        // Arrange
        _signInSettings = new AppSignInSettingsDto
        {
            AppId = 1,
            RequireConfirmedAccount = true,
            DefaultRole = "user",
        };
        var command = GetRegisterUserCommand();
        command.RequestContext = RequestContext.Create("203.0.113.7", "Test Browser");
        string? userId = null;

        SetUpSuccessfulSignUp(command);
        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._))
            .Invokes((IdentityUser user) => userId = user.Id)
            .Returns(IdentityResult.Success);

        // Act
        var vm = await GetHandler().CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SignInStep.ConfirmEmail, vm.NextStep);
        Assert.Equal("login-ticket", vm.Ticket);
        Assert.Equal("t***@domain.com", vm.CodeSentTo);
        Assert.Equal(userId, vm.User.Id);
        A.CallTo(() =>
                _createOutboxMessageCommandHandler.CreateItemAsync(
                    A<CreateOutboxMessageCommand>.That.Matches(c =>
                        c.Name == UserMessages.UserRegistered
                        && c.Message.Action == UserMessages.WelcomeUserAction
                        && c.Message.CorrelationId == userId
                        && c.Message.GetData<UserMessageData>()!.FirstName == command.FirstName
                        && c.Message.GetData<UserMessageData>()!.IpAddress == "203.0.113.7"
                    ),
                    userId!,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() =>
                _createLoginTicketCommandHandler.CreateItemAsync(
                    A<CreateLoginTicketCommand>.That.Matches(c =>
                        c.UserId == userId
                        && c.AppId == _registeredApp.Id
                        && c.Step == SignInStep.ConfirmEmail
                        && c.CodeAction == UserMessages.WelcomeUserAction
                        && c.CodeCorrelationId == userId
                    ),
                    userId!,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _signInUserCommandHandler.CreateItemAsync(A<SignInUserCommand>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Sign_Up_Creates_The_Details_And_The_App_Link_Audited_By_The_New_User()
    {
        // Arrange
        var command = GetRegisterUserCommand();
        string? userId = null;

        SetUpSuccessfulSignUp(command);
        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._))
            .Invokes((IdentityUser user) => userId = user.Id)
            .Returns(IdentityResult.Success);

        // Act
        await GetHandler().CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() =>
                _userRepository.CreateEntityItem(
                    A<UserEntity>.That.Matches(e =>
                        e.Id == userId && e.FirstName == command.FirstName && e.LastName == command.LastName && e.CreatedBy == userId
                    )
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() =>
                _userRegisteredAppRepository.CreateEntityItem(
                    A<UserRegisteredAppEntity>.That.Matches(e =>
                        e.UserId == userId && e.AppId == _registeredApp.Id && e.CreatedBy == userId
                    )
                )
            )
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Sign_Up_With_An_Invite_Code_Marks_It_Used_And_Grants_Its_Role()
    {
        // Arrange
        const string inviteRole = "testapp:owner";
        _signInSettings = new AppSignInSettingsDto
        {
            AppId = 1,
            RequireInviteCode = true,
            DefaultRole = "user",
        };
        var command = GetRegisterUserCommand() with { InviteCode = "invite01" };
        var inviteCodeEntity = new InviteCodeEntity
        {
            Id = 7,
            InviteCode = "INVITE01",
            UserEmail = command.UserEmail!,
            Role = inviteRole,
        };
        string? userId = null;

        SetUpSuccessfulSignUp(command);
        SetUpInvite(command, inviteCodeEntity);
        A.CallTo(() => _roleManager.FindByNameAsync(inviteRole)).Returns(new IdentityRole(inviteRole));
        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._))
            .Invokes((IdentityUser user) => userId = user.Id)
            .Returns(IdentityResult.Success);

        // Act
        await GetHandler().CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(inviteCodeEntity.Applied);
        Assert.Equal(userId, inviteCodeEntity.UpdatedBy);
        string[] expectedRoles = [DefaultRoleName, inviteRole];
        A.CallTo(() => _userManager.AddToRolesAsync(A<IdentityUser>._, A<IEnumerable<string>>.That.IsSameSequenceAs(expectedRoles)))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _inviteCodeRepository.UpdateEntityItem(inviteCodeEntity)).MustHaveHappenedOnceExactly();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong")]
    [InlineData("used")]
    public async Task Sign_Up_With_A_Missing_Wrong_Or_Used_Invite_Code_Gives_The_Same_Error(string inviteState)
    {
        // Arrange
        var command = GetRegisterUserCommand() with
        {
            InviteCode = "ABCDEFGHJK",
        };
        var inviteCodeEntity = inviteState switch
        {
            "missing" => null,
            "wrong" => new InviteCodeEntity { InviteCode = "KJHGFEDCBA", UserEmail = command.UserEmail! },
            _ => new InviteCodeEntity
            {
                InviteCode = "ABCDEFGHJK",
                UserEmail = command.UserEmail!,
                Applied = true,
            },
        };

        SetUpSuccessfulSignUp(command);
        SetUpInvite(command, inviteCodeEntity);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Users.InviteCodeRequired, exception.Message);
        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Sign_Up_Without_An_Invite_Code_When_The_App_Requires_One_Throws_InvalidDataException()
    {
        // Arrange
        _signInSettings = new AppSignInSettingsDto { AppId = 1, RequireInviteCode = true };
        var command = GetRegisterUserCommand();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Users.InviteCodeRequired, exception.Message);
    }

    [Fact]
    public async Task Sign_Up_When_The_Invites_Role_Is_Missing_Throws_And_Rolls_Back()
    {
        // Arrange
        const string inviteRole = "testapp:owner";
        var command = GetRegisterUserCommand() with { InviteCode = "INVITE01" };
        var inviteCodeEntity = new InviteCodeEntity
        {
            InviteCode = "INVITE01",
            UserEmail = command.UserEmail!,
            Role = inviteRole,
        };

        SetUpSuccessfulSignUp(command);
        SetUpInvite(command, inviteCodeEntity);
        A.CallTo(() => _roleManager.FindByNameAsync(inviteRole)).Returns((IdentityRole?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            GetHandler().CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        Assert.Equal(ErrorMessages.Users.InviteRoleMissing(inviteRole), exception.Message);
        A.CallTo(() => _unitOfWork.RollbackTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Sign_Up_For_An_App_Without_A_Default_Role_Grants_No_Role()
    {
        // Arrange
        _signInSettings = new AppSignInSettingsDto { AppId = 1 };
        var command = GetRegisterUserCommand();

        SetUpSuccessfulSignUp(command);

        // Act
        await GetHandler().CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _roleManager.FindByNameAsync(A<string>._)).MustNotHaveHappened();
        A.CallTo(() => _userManager.AddToRolesAsync(A<IdentityUser>._, A<IEnumerable<string>>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Sign_Up_For_A_Service_Creates_The_Account_In_The_Signed_App_And_Publishes_Nothing()
    {
        // Arrange
        _signInSettings = new AppSignInSettingsDto
        {
            AppId = 1,
            RequireConfirmedAccount = true,
            DefaultRole = "user",
        };
        var command = GetRegisterServiceUserCommand();
        string? userId = null;

        SetUpSuccessfulSignUp(command);
        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._))
            .Invokes((IdentityUser user) => userId = user.Id)
            .Returns(IdentityResult.Success);

        // Act
        var vm = await GetHandler().CreateItemAsync(command, KnownUsers.SystemId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(userId, vm.User.Id);
        Assert.Equal(command.UserEmail, vm.User.Email);
        A.CallTo(() =>
                _userRegisteredAppRepository.CreateEntityItem(
                    A<UserRegisteredAppEntity>.That.Matches(e => e.AppId == _registeredApp.Id && e.CreatedBy == KnownUsers.SystemId)
                )
            )
            .MustHaveHappenedOnceExactly();
        A.CallTo(() =>
                _createOutboxMessageCommandHandler.CreateItemAsync(A<CreateOutboxMessageCommand>._, A<string>._, A<CancellationToken>._)
            )
            .MustNotHaveHappened();
        A.CallTo(() => _createLoginTicketCommandHandler.CreateItemAsync(A<CreateLoginTicketCommand>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => _signInUserCommandHandler.CreateItemAsync(A<SignInUserCommand>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => _unitOfWork.CommitTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Sign_Up_For_A_Service_With_A_Taken_Email_Throws_RecordExistsException()
    {
        // Arrange
        var command = GetRegisterServiceUserCommand();

        SetUpSuccessfulSignUp(command);
        A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).Returns(new IdentityUser { Id = "existing-user" });

        // Act & Assert
        await Assert.ThrowsAsync<RecordExistsException>(() =>
            GetHandler().CreateItemAsync(command, KnownUsers.SystemId, TestContext.Current.CancellationToken)
        );

        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._)).MustNotHaveHappened();
        A.CallTo(() => _unitOfWork.RollbackTransactionAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    private RegisterUserCommandHandler GetHandler() =>
        new(
            _userManager,
            _roleManager,
            _unitOfWork,
            _userRepository,
            _userRegisteredAppRepository,
            _inviteCodeRepository,
            _tokenFactory,
            _timeProvider,
            _getSignInSettingsQueryHandler,
            _createAccountLimitEventCommandHandler,
            _createOutboxMessageCommandHandler,
            _createLoginTicketCommandHandler,
            _signInUserCommandHandler
        );

    private void SetUpSuccessfulSignUp(RegisterUserInput input)
    {
        A.CallTo(() => _roleManager.FindByNameAsync(DefaultRoleName)).Returns(new IdentityRole(DefaultRoleName));
        A.CallTo(() => _userManager.FindByNameAsync(input.UserName)).Returns((IdentityUser?)null);
        A.CallTo(() => _userManager.FindByEmailAsync(input.UserEmail!)).Returns((IdentityUser?)null);
        A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._)).Returns(IdentityResult.Success);
        A.CallTo(() => _userManager.AddToRolesAsync(A<IdentityUser>._, A<IEnumerable<string>>._)).Returns(IdentityResult.Success);
    }

    private void SetUpInvite(RegisterUserInput input, InviteCodeEntity? inviteCodeEntity) =>
        A.CallTo(() =>
                _inviteCodeRepository.GetEntityByAlternateIdAsync(
                    A<InviteCodeEntity>.That.Matches(e => e.UserEmail == input.UserEmail && e.AppId == _registeredApp.Id),
                    QueryCondition.MayOrMayNotExist,
                    A<CancellationToken>._
                )
            )
            .Returns(inviteCodeEntity);

    private RegisterUserCommand GetRegisterUserCommand() =>
        new()
        {
            FirstName = "Test",
            LastName = "User",
            UserName = "test.user",
            PhoneNumber = "0712345678",
            UserEmail = "test.user@domain.com",
            UserPassword = "Password123",
            RegisteredApp = _registeredApp,
        };

    private RegisterServiceUserCommand GetRegisterServiceUserCommand() =>
        new()
        {
            FirstName = "Test",
            LastName = "User",
            UserName = "test.user",
            UserEmail = "test.user@domain.com",
            UserPassword = "Password123",
            RegisteredApp = _registeredApp,
        };
}
