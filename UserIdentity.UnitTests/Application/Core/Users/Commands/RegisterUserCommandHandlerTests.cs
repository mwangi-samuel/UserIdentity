using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Application.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Common.Utilities;
using PolyzenKit.Domain.RegisteredApps;
using PolyzenKit.Infrastructure.Security.Jwt;
using PolyzenKit.Infrastructure.Security.Tokens;
using PolyzenKit.Infrastructure.Utilities;
using PolyzenKit.Persistence.Repositories;
using PolyzenKit.Presentation.Settings;

using UserIdentity.Application.Core.Roles.Queries;
using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.Application.Core.Users.Commands;
using UserIdentity.Application.Core.Users.Events;
using UserIdentity.Application.Core.Users.Settings;
using UserIdentity.Application.Enums;
using UserIdentity.Application.Interfaces;
using UserIdentity.Domain.RefreshTokens;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.InviteCodes;
using UserIdentity.Persistence.Repositories.RefreshTokens;
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
  private readonly IRefreshTokenRepository _refreshTokenRepository;
  private readonly IUserRegisteredAppRepository _userRegisteredAppRepository;
  private readonly IInviteCodeRepository _inviteCodeRepository;
  private readonly IJwtTokenHandler _jwtTokenHandler;
  private readonly ITokenFactory _tokenFactory;
  private readonly IMachineDateTime _machineDateTime;
  private readonly IEventHandler<UserUpdateEvent> _userUpdateEventHandler;
  private readonly IOptions<GoogleRecaptchaSettings> _googleRecaptchaSettingsOptions;
  private readonly IGoogleRecaptchaService _googleRecaptchaService;
  private readonly IGetItemsQueryHandler<GetRoleClaimsForRolesQuery, RoleClaimsForRolesViewModels> _getRoleClaimsQueryHandler;

  public RegisterUserCommandHandlerTests()
  {
    _roleSettings = A.Fake<IOptions<RoleSettings>>();
    _identityOptions = A.Fake<IOptions<IdentityOptions>>();
    _userManager = IdentityFakes.UserManager();
    _roleManager = A.Fake<RoleManager<IdentityRole>>();
    _unitOfWork = A.Fake<IUnitOfWork>();
    _userRepository = A.Fake<IUserRepository>();
    _refreshTokenRepository = A.Fake<IRefreshTokenRepository>();
    _userRegisteredAppRepository = A.Fake<IUserRegisteredAppRepository>();
    _inviteCodeRepository = A.Fake<IInviteCodeRepository>();
    _jwtTokenHandler = A.Fake<IJwtTokenHandler>();
    _tokenFactory = A.Fake<ITokenFactory>();
    _machineDateTime = new MachineDateTime();
    _userUpdateEventHandler = A.Fake<IEventHandler<UserUpdateEvent>>();
    _googleRecaptchaSettingsOptions = A.Fake<IOptions<GoogleRecaptchaSettings>>();
    _googleRecaptchaService = A.Fake<IGoogleRecaptchaService>();
    _getRoleClaimsQueryHandler = A.Fake<IGetItemsQueryHandler<GetRoleClaimsForRolesQuery, RoleClaimsForRolesViewModels>>();

    A.CallTo(() => _googleRecaptchaSettingsOptions.Value).Returns(new GoogleRecaptchaSettings { Enabled = false });
    A.CallTo(() => _unitOfWork.BeginTransactionAsync()).Returns(Task.CompletedTask);
    A.CallTo(() => _unitOfWork.CommitTransactionAsync()).Returns(Task.CompletedTask);
    A.CallTo(() => _unitOfWork.RollbackTransactionAsync()).Returns(Task.CompletedTask);
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

    await Assert.ThrowsAsync<RecordCreationException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId));
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

    await Assert.ThrowsAsync<RecordExistsException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId));

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

    await Assert.ThrowsAsync<RecordExistsException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId));

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

    await Assert.ThrowsAsync<InvalidDataException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId));

    A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, command.UserPassword)).MustHaveHappenedOnceExactly();

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
    A.CallTo(() => _userManager.AddToRoleAsync(A<IdentityUser>._, defaultRole.Name!)).Returns(IdentityResult.Failed());

    var handler = GetRegisterUserCommandHandler();

    await Assert.ThrowsAsync<RecordCreationException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId));

    A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, command.UserPassword)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.AddToRoleAsync(A<IdentityUser>._, defaultRole.Name!)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Create_User_With_Valid_Details_Returns_AuthUser()
  {
    var command = GetRegisterUserCommand();
    var defaultRole = new IdentityRole(GetDefaultRoleName());
    var refreshToken = "refresh-token";
    var accessToken = "access-token";
    var expiresIn = 1200;

    string? userId = null;
    IdentityUser? createdUser = null;
    var roleClaims = new HashSet<string> { "role-claim-value" };

    A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).Returns(defaultRole);
    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).Returns((IdentityUser?)null);
    A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).Returns((IdentityUser?)null);
    A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, command.UserPassword))
        .Invokes((IdentityUser u, string _) =>
        {
          createdUser = u;
          userId = u.Id;
        })
        .Returns(IdentityResult.Success);
    A.CallTo(() => _userManager.AddToRoleAsync(A<IdentityUser>._, defaultRole.Name!)).Returns(IdentityResult.Success);
    A.CallTo(() => _userManager.GenerateEmailConfirmationTokenAsync(A<IdentityUser>._)).Returns("token");
    A.CallTo(() => _userRepository.CreateEntityItem(A<UserEntity>.That.Matches(u => u.FirstName == command.FirstName))).DoesNothing();
    A.CallTo(() => _getRoleClaimsQueryHandler.GetItemsAsync(A<GetRoleClaimsForRolesQuery>._)).Returns(new RoleClaimsForRolesViewModels { RoleClaims = roleClaims });
    A.CallTo(() => _jwtTokenHandler.CreateToken(A<string>._, A<string>._, A<string>._, A<HashSet<string>>._, A<HashSet<string>>._, A<HashSet<Claim>?>._)).Returns((accessToken, expiresIn));
    A.CallTo(() => _tokenFactory.GenerateToken(32)).Returns(refreshToken);
    A.CallTo(() => _refreshTokenRepository.CreateEntityItem(A<RefreshTokenEntity>._)).DoesNothing();
    A.CallTo(() => _unitOfWork.CommitTransactionAsync()).DoesNothing();
    A.CallTo(() => _userUpdateEventHandler.HandleEventAsync(A<UserUpdateEvent>.That.Matches(e => e.UserContent.FirstName == command.FirstName))).DoesNothing();

    var handler = GetRegisterUserCommandHandler();

    var vm = await handler.CreateItemAsync(command, userId!);

    Assert.NotNull(vm.User);
    Assert.Equal(userId, vm.User.Id);
    Assert.Equal(command.FirstName, vm.User.FirstName);
    Assert.Equal(command.LastName, vm.User.LastName);
    Assert.Equal(command.UserEmail, vm.User.Email);
    Assert.Equal([defaultRole.Name!], vm.User.Roles);
    Assert.Equal(roleClaims, vm.User.RoleClaims);
    Assert.NotNull(vm.UserToken);
    Assert.Equal(refreshToken, vm.UserToken?.RefreshToken);
    Assert.Equal(accessToken, vm.UserToken?.AccessToken?.Token);
    Assert.Equal(expiresIn, vm.UserToken?.AccessToken?.ExpiresIn);

    A.CallTo(() => _roleManager.FindByNameAsync(defaultRole.Name!)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.FindByNameAsync(command.UserName)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.FindByEmailAsync(command.UserEmail!)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.CreateAsync(A<IdentityUser>._, command.UserPassword)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.AddToRoleAsync(A<IdentityUser>._, defaultRole.Name!)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userManager.GenerateEmailConfirmationTokenAsync(A<IdentityUser>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userRepository.CreateEntityItem(A<UserEntity>.That.Matches(u => u.FirstName == command.FirstName))).MustHaveHappenedOnceExactly();
    A.CallTo(() => _getRoleClaimsQueryHandler.GetItemsAsync(A<GetRoleClaimsForRolesQuery>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _jwtTokenHandler.CreateToken(A<string>._, A<string>._, A<string>._, A<HashSet<string>>._, A<HashSet<string>>._, A<HashSet<Claim>?>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _tokenFactory.GenerateToken(32)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _refreshTokenRepository.CreateEntityItem(A<RefreshTokenEntity>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _refreshTokenRepository.CreateEntityItem(A<RefreshTokenEntity>.That.Matches(rt => rt.Token == refreshToken))).MustHaveHappenedOnceExactly();
    A.CallTo(() => _unitOfWork.CommitTransactionAsync()).MustHaveHappenedOnceExactly();
    A.CallTo(() => _userUpdateEventHandler.HandleEventAsync(A<UserUpdateEvent>.That.Matches(e => e.UserContent.FirstName == command.FirstName))).MustHaveHappenedOnceExactly();
  }

  private RegisterUserCommandHandler GetRegisterUserCommandHandler()
  {
    return new RegisterUserCommandHandler(
        _roleSettings,
        _identityOptions,
        _userManager,
        _roleManager,
        _unitOfWork,
        _userRepository,
        _refreshTokenRepository,
        _userRegisteredAppRepository,
        _inviteCodeRepository,
        _jwtTokenHandler,
        _tokenFactory,
        _machineDateTime,
        _userUpdateEventHandler,
        _googleRecaptchaSettingsOptions,
        _googleRecaptchaService,
        _getRoleClaimsQueryHandler
    );
  }

  private string GetDefaultRoleName()
  {
    var settings = _roleSettings.Value;
    var command = GetRegisterUserCommand();
    return $"{command.RegisteredApp!.AppName.Trim().ToLower()}{ZenConstants.SERVICE_ROLE_SEPARATOR}{settings.DefaultRole.Trim().ToLower()}";
  }

  private RegisterUserCommand GetRegisterUserCommand()
  {
    return new RegisterUserCommand
    {
      FirstName = "Test",
      LastName = "User",
      UserName = "test.user",
      PhoneNumber = "0712345678",
      UserEmail = "test.user@domain.com",
      UserPassword = "Password123",
      RegisteredApp = new RegisteredAppEntity { Id = 1, AppName = "TestApp" },
      RequestSource = RequestSource.API
    };
  }
}
