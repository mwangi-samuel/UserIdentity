using System.Security.Authentication;
using System.Security.Claims;

using FakeItEasy;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Settings;
using PolyzenKit.Domain.Entities.RegisteredApps;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.Tokens.Shared;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Domain.RefreshTokens;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using UserIdentity.Persistence.Repositories.Roles;
using UserIdentity.Persistence.Repositories.Users;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class LoginUserCommandHandlerTests
{
  private static readonly string[] TestAppUserRoles = ["testapp:user"];
  private static readonly string[] UserReadScopes = ["useridentity-user:read"];

  private readonly IOptions<IdentityOptions> _identityOptions = Options.Create(new IdentityOptions());
  private readonly IOptions<RoleSettings> _roleSettings = Options.Create(new RoleSettings { ServiceName = "UserIdentity" });
  private readonly IOptions<RefreshTokenSettings> _refreshTokenSettings = Options.Create(new RefreshTokenSettings { ValidForSeconds = 2592000 });
  private readonly IJwtTokenHandler _jwtTokenHandler = A.Fake<IJwtTokenHandler>();
  private readonly ITokenFactory _tokenFactory = A.Fake<ITokenFactory>();
  private readonly UserManager<IdentityUser> _userManager = IdentityFakes.UserManager();
  private readonly IUnitOfWork _unitOfWork = A.Fake<IUnitOfWork>();
  private readonly IUserRepository _userRepository = A.Fake<IUserRepository>();
  private readonly IRoleRepository _roleRepository = A.Fake<IRoleRepository>();
  private readonly IRefreshTokenRepository _refreshTokenRepository = A.Fake<IRefreshTokenRepository>();
  private readonly TimeProvider _timeProvider = TimeProvider.System;

  private readonly RegisteredAppEntity _registeredApp = new() { AppName = "TestApp" };

  [Fact]
  public async Task Login_With_Non_Existent_User_In_UserManager_Throws_InvalidCredentialException()
  {
    // Arrange
    var command = GetCommand();

    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(default(IdentityUser));
    A.CallTo(() => _userManager.FindByEmailAsync(command.UserName)).Returns(default(IdentityUser));

    var handler = GetLoginUserCommandHandler();

    // Act & Assert
    var exception = await Assert.ThrowsAsync<InvalidCredentialException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));

    Assert.DoesNotContain(command.UserName, exception.Message);
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

    var handler = GetLoginUserCommandHandler();

    // Act & Assert
    var exception = await Assert.ThrowsAsync<InvalidCredentialException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));

    Assert.DoesNotContain(command.UserName, exception.Message);
    A.CallTo(() => _userManager.AccessFailedAsync(existingIdentityUser)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.ResetAccessFailedCountAsync(A<IdentityUser>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Login_With_Locked_Out_User_Throws_InvalidCredentialException_Without_Checking_Password()
  {
    // Arrange
    var command = GetCommand();
    var existingIdentityUser = GetIdentityUser();

    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(existingIdentityUser);
    A.CallTo(() => _userManager.IsLockedOutAsync(existingIdentityUser)).Returns(true);

    var handler = GetLoginUserCommandHandler();

    // Act & Assert
    await Assert.ThrowsAsync<InvalidCredentialException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));

    A.CallTo(() => _userManager.CheckPasswordAsync(A<IdentityUser>._, A<string>._)).MustNotHaveHappened();
    A.CallTo(() => _userManager.AccessFailedAsync(A<IdentityUser>._)).MustNotHaveHappened();
    A.CallTo(() => _refreshTokenRepository.CreateEntityItem(A<RefreshTokenEntity>._)).MustNotHaveHappened();
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
    A.CallTo(() => _userRepository.GetUserDtoAsync(existingIdentityUser.Id, "TestApp", A<CancellationToken>._)).Returns((UserDto?)null);

    var handler = GetLoginUserCommandHandler();

    // Act & Assert
    await Assert.ThrowsAsync<InvalidCredentialException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));

    A.CallTo(() => _refreshTokenRepository.CreateEntityItem(A<RefreshTokenEntity>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Login_With_Valid_Details_Starts_A_Session_With_The_Apps_Roles_And_Scopes()
  {
    // Arrange
    var command = GetCommand();
    var existingIdentityUser = GetIdentityUser();
    var userDto = new UserDto { Id = existingIdentityUser.Id, FirstName = "Test", UserName = existingIdentityUser.UserName };

    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(existingIdentityUser);
    A.CallTo(() => _userManager.CheckPasswordAsync(existingIdentityUser, command.Password)).Returns(true);
    A.CallTo(() => _userRepository.GetUserDtoAsync(existingIdentityUser.Id, "TestApp", A<CancellationToken>._)).Returns(userDto);
    A.CallTo(() => _roleRepository.GetUserRoleNamesAsync(existingIdentityUser.Id, A<CancellationToken>._)).Returns(["otherapp:admin", "testapp:user"]);
    A.CallTo(() => _roleRepository.GetScopesAsync(A<IReadOnlyCollection<string>>.That.IsSameSequenceAs(TestAppUserRoles), A<CancellationToken>._)).Returns(["useridentity-user:read"]);
    A.CallTo(() => _tokenFactory.GenerateToken(32)).Returns("refresh-token");
    A.CallTo(() => _tokenFactory.HashToken("refresh-token")).Returns("refresh-token-hash");
    A.CallTo(() => _jwtTokenHandler.CreateToken(existingIdentityUser.Id, existingIdentityUser.UserName!, "TestApp",
        A<HashSet<string>>.That.IsSameSequenceAs(TestAppUserRoles), A<HashSet<string>>.That.IsSameSequenceAs(UserReadScopes), A<HashSet<Claim>?>._))
        .Returns(("access-token", 180));

    var handler = GetLoginUserCommandHandler();

    // Act
    var vm = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.Same(userDto, vm.User);
    Assert.True(vm.IsConfirmed);
    Assert.Equal("access-token", vm.UserToken?.AccessToken?.Token);
    Assert.Equal(180, vm.UserToken?.AccessToken?.ExpiresIn);
    Assert.Equal("refresh-token", vm.UserToken?.RefreshToken);

    A.CallTo(() => _refreshTokenRepository.CreateEntityItem(A<RefreshTokenEntity>.That.Matches(rt =>
        rt.Token == "refresh-token-hash" && rt.UserId == existingIdentityUser.Id && rt.Expires > DateTime.UtcNow.AddDays(29)))).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.ResetAccessFailedCountAsync(existingIdentityUser)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.FindByIdAsync(A<string>._)).MustNotHaveHappened();
    A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Login_Of_An_Unconfirmed_User_When_Confirmation_Is_Required_Returns_No_Token()
  {
    // Arrange
    var command = GetCommand();
    var existingIdentityUser = GetIdentityUser();
    var identityOptions = new IdentityOptions();
    identityOptions.SignIn.RequireConfirmedAccount = true;

    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(existingIdentityUser);
    A.CallTo(() => _userManager.CheckPasswordAsync(existingIdentityUser, command.Password)).Returns(true);
    A.CallTo(() => _userRepository.GetUserDtoAsync(existingIdentityUser.Id, "TestApp", A<CancellationToken>._)).Returns(new UserDto { Id = existingIdentityUser.Id });

    var handler = GetLoginUserCommandHandler(Options.Create(identityOptions));

    // Act
    var vm = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.False(vm.IsConfirmed);
    Assert.Null(vm.UserToken);
    A.CallTo(() => _refreshTokenRepository.CreateEntityItem(A<RefreshTokenEntity>._)).MustNotHaveHappened();
    A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
  }

  private LoginUserCommand GetCommand() => new()
  {
    UserName = "test",
    Password = "test",
    RegisteredApp = _registeredApp
  };

  private LoginUserCommandHandler GetLoginUserCommandHandler(IOptions<IdentityOptions>? identityOptions = null) => new(
    identityOptions ?? _identityOptions,
    _roleSettings,
    _refreshTokenSettings,
    _jwtTokenHandler,
    _tokenFactory,
    _userManager,
    _unitOfWork,
    _userRepository,
    _roleRepository,
    _refreshTokenRepository,
    _timeProvider
  );

  private static IdentityUser GetIdentityUser() => new()
  {
    Id = Guid.NewGuid().ToString(),
    UserName = "test",
    Email = "test@pl.pl"
  };
}
