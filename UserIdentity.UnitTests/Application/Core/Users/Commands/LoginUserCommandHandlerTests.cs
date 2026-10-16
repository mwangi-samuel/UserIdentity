using System;
using System.Collections.Generic;
using System.Security.Authentication;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Domain.RegisteredApps;
using PolyzenKit.Infrastructure.Security.Jwt;
using PolyzenKit.Infrastructure.Security.Tokens;
using PolyzenKit.Infrastructure.Utilities;
using PolyzenKit.Persistence.Repositories;

using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Queries;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Domain.RefreshTokens;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.RefreshTokens;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Commands;

public class LoginUserCommandHandlerTests
{
  private readonly IOptions<IdentityOptions> _identityOptions;
  private readonly IJwtTokenHandler _jwtTokenHandler;
  private readonly ITokenFactory _tokenFactory;
  private readonly UserManager<IdentityUser> _userManager;
  private readonly IUnitOfWork _unitOfWork;
  private readonly IRefreshTokenRepository _refreshTokenRepository;
  private readonly IMachineDateTime _machineDateTime;
  private readonly IUserRegisteredAppRepository _userRegisteredAppRepository;

  private readonly IGetItemQueryHandler<GetUserQuery, UserViewModel> _getUserQueryHandler;


  public LoginUserCommandHandlerTests()
  {
    _identityOptions = A.Fake<IOptions<IdentityOptions>>();
    _jwtTokenHandler = A.Fake<IJwtTokenHandler>();
    _tokenFactory = A.Fake<ITokenFactory>();
    _userManager = IdentityFakes.UserManager();
    _unitOfWork = A.Fake<IUnitOfWork>();
    _refreshTokenRepository = A.Fake<IRefreshTokenRepository>();
    _machineDateTime = new MachineDateTime();
    _getUserQueryHandler = A.Fake<IGetItemQueryHandler<GetUserQuery, UserViewModel>>();
    _userRegisteredAppRepository = A.Fake<IUserRegisteredAppRepository>();

  }


  [Fact]
  public async Task Login_With_Non_Existent_User_In_UserManager_Throws_InvalidCredentialException()
  {
    // Arrange
    LoginUserCommand command = new()
    {
      UserName = "test",
      Password = "test"
    };

    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(default(IdentityUser));
    A.CallTo(() => _userManager.FindByEmailAsync(command.UserName)).Returns(default(IdentityUser));

    var handler = GetLoginUserCommandHandler();

    // Act & Assert
    await Assert.ThrowsAsync<InvalidCredentialException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId));
  }


  [Fact]
  public async Task Login_With_Existing_And_No_Matching_Password_Throws_InvalidCredentialException()
  {
    // Arrange
    LoginUserCommand command = new()
    {
      UserName = "test",
      Password = "test"
    };

    var existingIdentityUser = GetIdentityUser();

    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(default(IdentityUser));
    A.CallTo(() => _userManager.FindByEmailAsync(command.UserName)).Returns(existingIdentityUser);

    A.CallTo(() => _userManager.CheckPasswordAsync(existingIdentityUser, command.Password)).Returns(false);

    var handler = GetLoginUserCommandHandler();

    // Act & Assert
    await Assert.ThrowsAsync<InvalidCredentialException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId));
  }

  [Fact]
  public async Task Login_With_User_Record_Missing_Throws_InvalidCredentialException()
  {
    // Arrange
    LoginUserCommand command = new()
    {
      UserName = "test",
      Password = "test"
    };

    var existingIdentityUser = GetIdentityUser();

    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(default(IdentityUser));
    A.CallTo(() => _userManager.FindByEmailAsync(command.UserName)).Returns(existingIdentityUser);

    A.CallTo(() => _userManager.CheckPasswordAsync(existingIdentityUser, command.Password)).Returns(true);

    A.CallTo(() => _getUserQueryHandler.GetItemAsync(A<GetUserQuery>.That.Matches(q => q.UserId == existingIdentityUser.Id)))
        .Throws(new NoRecordException(existingIdentityUser.Id, "User"));

    var handler = GetLoginUserCommandHandler();

    // Act & Assert
    await Assert.ThrowsAsync<InvalidCredentialException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId));
  }

  [Fact]
  public async Task Login_With_Valid_Details_Returns_Valid_Authenticated_User()
  {
    // Arrange
    var registeredApp = new RegisteredAppEntity { AppName = "TestApp" };
    LoginUserCommand command = new()
    {
      UserName = "test",
      Password = "test",
      RegisteredApp = registeredApp
    };

    var existingIdentityUser = GetIdentityUser();
    var refreshToken = "sampleRefreshToken";
    var accessToken = "access-token";
    var expiresIn = 3600;
    var userDto = new UserDTO
    {
      Id = existingIdentityUser.Id,
      FirstName = "Test",
      LastName = "User",
      UserName = existingIdentityUser.UserName,
      Email = existingIdentityUser.Email,
      Roles = ["admin"],
      RoleClaims = ["scope:user:read"]
    };

    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns(default(IdentityUser));
    A.CallTo(() => _userManager.FindByEmailAsync(command.UserName)).Returns(existingIdentityUser);

    A.CallTo(() => _userManager.CheckPasswordAsync(existingIdentityUser, command.Password)).Returns(true);
    A.CallTo(() => _getUserQueryHandler.GetItemAsync(A<GetUserQuery>.That.Matches(q => q.UserId == existingIdentityUser.Id)))
        .Returns(new UserViewModel { User = userDto });

    A.CallTo(() => _tokenFactory.GenerateToken(32)).Returns(refreshToken);
    A.CallTo(() => _jwtTokenHandler.CreateToken(existingIdentityUser.Id, existingIdentityUser.UserName!, registeredApp.AppName, userDto.Roles, userDto.RoleClaims, A<HashSet<Claim>?>._))
        .Returns((accessToken, expiresIn));

    var handler = GetLoginUserCommandHandler();

    // Act 
    var vm = await handler.CreateItemAsync(command, TestStringHelper.UserId);

    // Assert
    Assert.IsType<AuthUserViewModel>(vm);
    Assert.NotNull(vm.User);
    Assert.NotNull(vm.UserToken);
    Assert.NotNull(vm.UserToken?.AccessToken);
    Assert.NotNull(vm.UserToken?.RefreshToken);
    Assert.Equal(refreshToken, vm.UserToken?.RefreshToken);
    A.CallTo(() => _refreshTokenRepository.CreateEntityItem(A<RefreshTokenEntity>.That.Matches(rt => rt.Token == refreshToken && rt.UserId == existingIdentityUser.Id))).MustHaveHappenedOnceExactly();
    A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }


  private LoginUserCommandHandler GetLoginUserCommandHandler()
  {
    return new LoginUserCommandHandler(
        _identityOptions,
        _jwtTokenHandler,
        _tokenFactory,
        _userManager,
        _unitOfWork,
        _refreshTokenRepository,
        _machineDateTime,
        _getUserQueryHandler,
        _userRegisteredAppRepository
    );
  }

  private IdentityUser GetIdentityUser()
  {
    return new IdentityUser
    {
      Id = Guid.NewGuid().ToString(),
      UserName = "test",
      Email = "test@pl.pl"
    };

  }

}
