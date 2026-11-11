using System.Security.Authentication;
using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Core.RegisteredApps.Shared;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.Tokens.Commands;
using UserIdentity.Application.Core.Tokens.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.Users;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class LoginUserCommandHandlerTests
{
    private readonly IOptions<IdentityOptions> _identityOptions = Options.Create(new IdentityOptions());
    private readonly UserManager<IdentityUser> _userManager = IdentityFakes.UserManager();
    private readonly IUnitOfWork _unitOfWork = A.Fake<IUnitOfWork>();
    private readonly IUserRepository _userRepository = A.Fake<IUserRepository>();
    private readonly ICreateItemCommandHandler<StartSessionCommand, UserTokenViewModel> _startSessionCommandHandler = A.Fake<
        ICreateItemCommandHandler<StartSessionCommand, UserTokenViewModel>
    >();
    private readonly ICreateItemCommandHandler<CreateLoginTicketCommand, LoginTicketViewModel> _createLoginTicketCommandHandler = A.Fake<
        ICreateItemCommandHandler<CreateLoginTicketCommand, LoginTicketViewModel>
    >();

    private readonly IPasswordHasher<IdentityUser> _passwordHasher = A.Fake<IPasswordHasher<IdentityUser>>();

    private readonly ActiveRegisteredApp _registeredApp = new()
    {
        Id = 1,
        AppName = "TestApp",
        AppSecretKey = "test-secret",
        BaseUrl = "https://app.test",
    };

    public LoginUserCommandHandlerTests()
    {
        _userManager.PasswordHasher = _passwordHasher;
    }

    [Fact]
    public async Task Login_With_Non_Existent_User_In_UserManager_Throws_InvalidCredentialException_After_Hashing_The_Password()
    {
        // Arrange
        var command = GetCommand();

        A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(default(IdentityUser));
        A.CallTo(() => _userManager.FindByEmailAsync(command.UserName)).Returns(default(IdentityUser));

        var handler = GetLoginUserCommandHandler();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidCredentialException>(() =>
            handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        Assert.DoesNotContain(command.UserName, exception.Message);
        A.CallTo(() => _passwordHasher.VerifyHashedPassword(A<IdentityUser>._, A<string>.That.Not.IsNullOrEmpty(), command.Password))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Login_With_Existing_And_No_Matching_Password_Throws_InvalidCredentialException()
    {
        // Arrange
        var command = GetCommand();
        var existingIdentityUser = GetIdentityUser();

        A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(default(IdentityUser));
        A.CallTo(() => _userManager.FindByEmailAsync(command.UserName)).Returns(existingIdentityUser);
        A.CallTo(() => _userManager.CheckPasswordAsync(existingIdentityUser, command.Password)).Returns(false);
        A.CallTo(() => _userManager.AccessFailedAsync(existingIdentityUser)).Returns(IdentityResult.Success);

        var handler = GetLoginUserCommandHandler();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidCredentialException>(() =>
            handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        Assert.DoesNotContain(command.UserName, exception.Message);
        A.CallTo(() => _userManager.AccessFailedAsync(existingIdentityUser)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _userManager.ResetAccessFailedCountAsync(A<IdentityUser>._)).MustNotHaveHappened();
        A.CallTo(() => _userRepository.ClearTrackedEntities()).MustNotHaveHappened();
    }

    [Fact]
    public async Task Login_With_No_Matching_Password_When_Another_Failed_Login_Saved_First_Counts_The_Attempt_On_The_Reloaded_User()
    {
        // Arrange
        var command = GetCommand();
        var existingIdentityUser = GetIdentityUser();
        var reloadedIdentityUser = GetIdentityUser();
        reloadedIdentityUser.Id = existingIdentityUser.Id;

        A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(existingIdentityUser);
        A.CallTo(() => _userManager.CheckPasswordAsync(existingIdentityUser, command.Password)).Returns(false);
        A.CallTo(() => _userManager.AccessFailedAsync(existingIdentityUser))
            .Returns(IdentityResult.Failed(new IdentityErrorDescriber().ConcurrencyFailure()));
        A.CallTo(() => _userManager.FindByIdAsync(existingIdentityUser.Id)).Returns(reloadedIdentityUser);
        A.CallTo(() => _userManager.AccessFailedAsync(reloadedIdentityUser)).Returns(IdentityResult.Success);

        var handler = GetLoginUserCommandHandler();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialException>(() =>
            handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        A.CallTo(() => _userManager.AccessFailedAsync(existingIdentityUser))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _userRepository.ClearTrackedEntities()).MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _userManager.FindByIdAsync(existingIdentityUser.Id)).MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _userManager.AccessFailedAsync(reloadedIdentityUser)).MustHaveHappenedOnceExactly());
        AssertNothingIssued();
    }

    [Fact]
    public async Task Login_With_No_Matching_Password_When_The_Failed_Attempt_Cant_Be_Counted_Throws_InvalidOperationException()
    {
        // Arrange
        var command = GetCommand();
        var existingIdentityUser = GetIdentityUser();

        A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(existingIdentityUser);
        A.CallTo(() => _userManager.CheckPasswordAsync(existingIdentityUser, command.Password)).Returns(false);
        A.CallTo(() => _userManager.AccessFailedAsync(existingIdentityUser))
            .Returns(IdentityResult.Failed(new IdentityError { Code = "Other", Description = "nope" }));

        var handler = GetLoginUserCommandHandler();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        A.CallTo(() => _userManager.AccessFailedAsync(A<IdentityUser>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _userRepository.ClearTrackedEntities()).MustNotHaveHappened();
        AssertNothingIssued();
    }

    [Fact]
    public async Task Login_With_Locked_Out_User_Throws_InvalidCredentialException_After_Hashing_The_Password_Without_Checking_It()
    {
        // Arrange
        var command = GetCommand();
        var existingIdentityUser = GetIdentityUser();

        A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(existingIdentityUser);
        A.CallTo(() => _userManager.IsLockedOutAsync(existingIdentityUser)).Returns(true);

        var handler = GetLoginUserCommandHandler();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialException>(() =>
            handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        A.CallTo(() => _passwordHasher.VerifyHashedPassword(A<IdentityUser>._, A<string>.That.Not.IsNullOrEmpty(), command.Password))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _userManager.CheckPasswordAsync(A<IdentityUser>._, A<string>._)).MustNotHaveHappened();
        A.CallTo(() => _userManager.AccessFailedAsync(A<IdentityUser>._)).MustNotHaveHappened();
        AssertNothingIssued();
    }

    [Fact]
    public async Task Login_Without_A_Profile_For_The_App_Throws_InvalidCredentialException()
    {
        // Arrange
        var command = GetCommand();
        var existingIdentityUser = GetIdentityUser();

        A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(existingIdentityUser);
        A.CallTo(() => _userManager.CheckPasswordAsync(existingIdentityUser, command.Password)).Returns(true);

        // Also what a user of another app gets, since the read is scoped to the login's app
        A.CallTo(() => _userRepository.GetUserDtoAsync(existingIdentityUser.Id, "TestApp", A<CancellationToken>._))
            .Returns((UserDto?)null);

        var handler = GetLoginUserCommandHandler();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialException>(() =>
            handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        AssertNothingIssued();
    }

    [Fact]
    public async Task Login_With_Valid_Details_Starts_A_Session_For_The_Apps_Login()
    {
        // Arrange
        var command = GetCommand();
        var existingIdentityUser = GetIdentityUser();
        var userDto = new UserDto
        {
            Id = existingIdentityUser.Id,
            FirstName = "Test",
            UserName = existingIdentityUser.UserName,
        };
        var session = new UserTokenViewModel
        {
            UserToken = new UserTokenDto
            {
                AccessToken = new AccessTokenDto { Token = "access-token", ExpiresIn = 180 },
                RefreshToken = "refresh-token",
            },
        };

        A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(existingIdentityUser);
        A.CallTo(() => _userManager.CheckPasswordAsync(existingIdentityUser, command.Password)).Returns(true);
        A.CallTo(() => _userRepository.GetUserDtoAsync(existingIdentityUser.Id, "TestApp", A<CancellationToken>._)).Returns(userDto);
        A.CallTo(() =>
                _startSessionCommandHandler.CreateItemAsync(
                    new StartSessionCommand(existingIdentityUser.Id, existingIdentityUser.UserName!, "TestApp"),
                    A<string>._,
                    A<CancellationToken>._
                )
            )
            .Returns(session);

        var handler = GetLoginUserCommandHandler();

        // Act
        var vm = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(userDto, vm.User);
        Assert.True(vm.IsConfirmed);
        Assert.Same(session.UserToken, vm.UserToken);
        Assert.Null(vm.LoginTicket);

        A.CallTo(() => _userManager.ResetAccessFailedCountAsync(existingIdentityUser)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _userManager.FindByIdAsync(A<string>._)).MustNotHaveHappened();
        A.CallTo(() => _createLoginTicketCommandHandler.CreateItemAsync(A<CreateLoginTicketCommand>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Login_Of_An_Unconfirmed_User_When_Confirmation_Is_Required_Returns_A_Login_Ticket_And_No_Token()
    {
        // Arrange
        var command = GetCommand();
        var existingIdentityUser = GetIdentityUser();
        var identityOptions = new IdentityOptions();
        identityOptions.SignIn.RequireConfirmedAccount = true;

        A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(existingIdentityUser);
        A.CallTo(() => _userManager.CheckPasswordAsync(existingIdentityUser, command.Password)).Returns(true);
        A.CallTo(() => _userRepository.GetUserDtoAsync(existingIdentityUser.Id, "TestApp", A<CancellationToken>._))
            .Returns(new UserDto { Id = existingIdentityUser.Id });
        A.CallTo(() =>
                _createLoginTicketCommandHandler.CreateItemAsync(
                    new CreateLoginTicketCommand(existingIdentityUser.Id, _registeredApp.Id),
                    A<string>._,
                    A<CancellationToken>._
                )
            )
            .Returns(new LoginTicketViewModel { Ticket = "login-ticket" });

        var handler = GetLoginUserCommandHandler(Options.Create(identityOptions));

        // Act
        var vm = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(vm.IsConfirmed);
        Assert.Null(vm.UserToken);
        Assert.Equal("login-ticket", vm.LoginTicket);
        A.CallTo(() => _startSessionCommandHandler.CreateItemAsync(A<StartSessionCommand>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    private void AssertNothingIssued()
    {
        A.CallTo(() => _startSessionCommandHandler.CreateItemAsync(A<StartSessionCommand>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        A.CallTo(() => _createLoginTicketCommandHandler.CreateItemAsync(A<CreateLoginTicketCommand>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    private LoginUserCommand GetCommand() =>
        new()
        {
            UserName = "test",
            Password = "test",
            RegisteredApp = _registeredApp,
        };

    private LoginUserCommandHandler GetLoginUserCommandHandler(IOptions<IdentityOptions>? identityOptions = null) =>
        new(
            identityOptions ?? _identityOptions,
            _userManager,
            _unitOfWork,
            _userRepository,
            _startSessionCommandHandler,
            _createLoginTicketCommandHandler
        );

    private static IdentityUser GetIdentityUser() =>
        new()
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "test",
            Email = "test@pl.pl",
        };
}
