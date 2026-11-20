using System.Security.Authentication;
using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Persistence.Repositories;
using UserIdentity.Application.Core.AccountLimits.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Shared;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Common.Enums;
using UserIdentity.Domain.Users;
using UserIdentity.UnitTests.TestUtils;
using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class LoginUserCommandHandlerTests
{
    private readonly UserManager<IdentityUser> _userManager = IdentityFakes.UserManager();
    private readonly IUnitOfWork _unitOfWork = A.Fake<IUnitOfWork>();
    private readonly ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel> _recordFailedSignInCommandHandler = A.Fake<
        ICreateItemCommandHandler<RecordFailedSignInCommand, AccountLimitViewModel>
    >();
    private readonly ICreateItemCommandHandler<SignInUserCommand, AuthUserViewModel> _signInUserCommandHandler = A.Fake<
        ICreateItemCommandHandler<SignInUserCommand, AuthUserViewModel>
    >();

    private readonly IPasswordHasher<IdentityUser> _passwordHasher = A.Fake<IPasswordHasher<IdentityUser>>();

    public LoginUserCommandHandlerTests()
    {
        _userManager.PasswordHasher = _passwordHasher;
    }

    [Fact]
    public async Task Login_With_An_Unknown_User_Throws_InvalidCredentialException_After_Hashing_The_Password()
    {
        // Arrange
        var command = GetCommand();

        A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(default(IdentityUser));
        A.CallTo(() => _userManager.FindByEmailAsync(command.UserName)).Returns(default(IdentityUser));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidCredentialException>(() =>
            GetHandler().CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        Assert.DoesNotContain(command.UserName, exception.Message);
        A.CallTo(() => _passwordHasher.VerifyHashedPassword(A<IdentityUser>._, A<string>.That.Not.IsNullOrEmpty(), command.Password))
            .MustHaveHappenedOnceExactly();
        AssertNothingIssued();
    }

    [Fact]
    public async Task Login_With_A_Wrong_Password_Counts_A_Failed_First_Step()
    {
        // Arrange
        var command = GetCommand();
        var user = GetIdentityUser();

        A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(default(IdentityUser));
        A.CallTo(() => _userManager.FindByEmailAsync(command.UserName)).Returns(user);
        A.CallTo(() => _userManager.CheckPasswordAsync(user, command.Password)).Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialException>(() =>
            GetHandler().CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        A.CallTo(() =>
                _recordFailedSignInCommandHandler.CreateItemAsync(
                    new RecordFailedSignInCommand(user, false),
                    user.Id,
                    A<CancellationToken>._
                )
            )
            .MustHaveHappenedOnceExactly();
        AssertNothingIssued();
    }

    [Fact]
    public async Task Login_With_A_Locked_Out_User_Throws_InvalidCredentialException_Without_Checking_The_Password()
    {
        // Arrange
        var command = GetCommand();
        var user = GetIdentityUser();

        A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(user);
        A.CallTo(() => _userManager.IsLockedOutAsync(user)).Returns(true);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialException>(() =>
            GetHandler().CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken)
        );

        A.CallTo(() => _passwordHasher.VerifyHashedPassword(A<IdentityUser>._, A<string>.That.Not.IsNullOrEmpty(), command.Password))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _userManager.CheckPasswordAsync(A<IdentityUser>._, A<string>._)).MustNotHaveHappened();
        AssertNothingIssued();
    }

    [Fact]
    public async Task Login_With_The_Right_Password_Continues_Through_Sign_In_Without_Resetting_The_Failure_Count()
    {
        // Arrange
        var command = GetCommand();
        var user = GetIdentityUser();
        var signedIn = new AuthUserViewModel
        {
            User = new UserDto { Id = user.Id },
            NextStep = SignInStep.SecondFactor,
        };

        A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(user);
        A.CallTo(() => _userManager.CheckPasswordAsync(user, command.Password)).Returns(true);
        A.CallTo(() =>
                _signInUserCommandHandler.CreateItemAsync(
                    A<SignInUserCommand>.That.Matches(c =>
                        c.User == user && c.RegisteredApp == command.RegisteredApp && !c.EmailProven && !c.SecondFactorPassed
                    ),
                    user.Id,
                    A<CancellationToken>._
                )
            )
            .Returns(signedIn);

        // Act
        var vm = await GetHandler().CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(signedIn, vm);
        A.CallTo(() => _userManager.ResetAccessFailedCountAsync(A<IdentityUser>._)).MustNotHaveHappened();
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    private void AssertNothingIssued() =>
        A.CallTo(() => _signInUserCommandHandler.CreateItemAsync(A<SignInUserCommand>._, A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();

    private static LoginUserCommand GetCommand() =>
        new()
        {
            UserName = "test",
            Password = "test",
            RegisteredApp = new()
            {
                Id = 1,
                AppName = "TestApp",
                BaseUrl = "https://app.test",
            },
            RequestContext = new RequestContext(),
        };

    private LoginUserCommandHandler GetHandler() =>
        new(_userManager, _unitOfWork, _recordFailedSignInCommandHandler, _signInUserCommandHandler);

    private static IdentityUser GetIdentityUser() =>
        new()
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "test",
            Email = "test@example.com",
        };
}
