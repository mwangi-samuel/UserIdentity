using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;
using PolyzenKit.Presentation.Settings;

using UserIdentity.Application.Core.Roles.Queries;
using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.Application.Core.Users.Queries;
using UserIdentity.Application.Core.Users.ViewModels;
using UserIdentity.Domain.Users;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.Persistence.Repositories.Users;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Users.Queries;

public class GetUserQueryHandlerTests
{
  private readonly UserManager<IdentityUser> _userManager;
  private readonly IUserRepository _userRepository;
  private readonly IGetItemsQueryHandler<GetRoleClaimsForRolesQuery, RoleClaimsForRolesViewModels> _getRoleClaimsQueryHandler;
  private readonly IUserRegisteredAppRepository _userRegisteredAppRepository;
  private readonly IOptions<RoleSettings> _roleSettings = Options.Create(new RoleSettings { ServiceName = "UserIdentity", AdminRoles = "admin", DefaultRole = "user" });


  public GetUserQueryHandlerTests()
  {
    _userManager = IdentityFakes.UserManager();
    _userRepository = A.Fake<IUserRepository>();
    _getRoleClaimsQueryHandler = A.Fake<IGetItemsQueryHandler<GetRoleClaimsForRolesQuery, RoleClaimsForRolesViewModels>>();
    _userRegisteredAppRepository = A.Fake<IUserRegisteredAppRepository>();
  }

  [Fact]
  public async Task Get_User_When_Non_Existent_In_User_Manager_Throws_NoRecordException()
  {

    // Arrange
    GetUserQuery query = new()
    {
      UserId = "test"
    };

    A.CallTo(() => _userManager.FindByIdAsync(query.UserId)).Returns(default(IdentityUser));

    GetUserQueryHandler handler = new(_userManager, _userRepository, _userRegisteredAppRepository, _roleSettings, _getRoleClaimsQueryHandler);

    // Act & Assert
    await Assert.ThrowsAsync<NoRecordException>(() => handler.GetItemAsync(query, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Get_User_When_Non_Existent_In_User_Repo_Throws_NoRecordException()
  {

    // Arrange
    GetUserQuery query = new()
    {
      UserId = "test"
    };

    IdentityUser existingIdentityUser = new()
    {
      Id = "test",
      UserName = "test",
      Email = "test@lp.mll",
    };

    A.CallTo(() => _userManager.FindByIdAsync(query.UserId)).Returns(existingIdentityUser);
    A.CallTo(() => _userRepository.GetEntityItemAsync(query.UserId, A<CancellationToken>._)).Throws(new NoRecordException(query.UserId, "User"));

    GetUserQueryHandler handler = new(_userManager, _userRepository, _userRegisteredAppRepository, _roleSettings, _getRoleClaimsQueryHandler);

    // Act & Assert
    await Assert.ThrowsAsync<NoRecordException>(() => handler.GetItemAsync(query, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Get_User_Existing_User_Returns_User()
  {

    // Arrange
    GetUserQuery query = new()
    {
      UserId = "test"
    };

    IdentityUser existingIdentityUser = new()
    {
      Id = "test",
      UserName = "test",
      Email = "test@lp.mll",
    };

    UserEntity existingUser = new()
    {
      Id = existingIdentityUser.Id,
      FirstName = "test",
      LastName = "test",
    };

    A.CallTo(() => _userManager.FindByIdAsync(query.UserId)).Returns(existingIdentityUser);
    A.CallTo(() => _userRepository.GetEntityItemAsync(query.UserId, A<CancellationToken>._)).Returns(existingUser);
    A.CallTo(() => _userManager.GetRolesAsync(existingIdentityUser)).Returns(["admin"]);
    A.CallTo(() => _getRoleClaimsQueryHandler.GetItemsAsync(A<GetRoleClaimsForRolesQuery>.That.Matches(q => q.Roles.Contains("admin")), A<CancellationToken>._))
        .Returns(new RoleClaimsForRolesViewModels { RoleClaims = ["scope:user:read"] });

    GetUserQueryHandler handler = new(_userManager, _userRepository, _userRegisteredAppRepository, _roleSettings, _getRoleClaimsQueryHandler);

    // Act 
    var vm = await handler.GetItemAsync(query, TestContext.Current.CancellationToken);

    // Assert
    Assert.IsType<UserViewModel>(vm);
    Assert.IsType<UserDto>(vm.User);
    Assert.Equal(existingUser.Id, vm.User?.Id);
    Assert.Equal("admin", vm.User?.Roles.First());
    Assert.Contains("scope:user:read", vm.User?.RoleClaims ?? []);
  }

  [Fact]
  public async Task Get_User_For_An_App_Returns_Only_The_Apps_Roles_And_Their_Claims()
  {
    // Arrange
    GetUserQuery query = new() { UserId = "test", AppName = "TestApp" };
    IdentityUser existingIdentityUser = new() { Id = "test", UserName = "test", Email = "test@lp.mll" };

    A.CallTo(() => _userManager.FindByIdAsync(query.UserId)).Returns(existingIdentityUser);
    A.CallTo(() => _userRegisteredAppRepository.IsUserRegisteredForAppAsync("test", "TestApp", A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _userRepository.GetEntityItemAsync(query.UserId, A<CancellationToken>._)).Returns(new UserEntity { Id = "test" });
    A.CallTo(() => _userManager.GetRolesAsync(existingIdentityUser)).Returns(["testapp:user", "otherapp:admin"]);

    GetUserQueryHandler handler = new(_userManager, _userRepository, _userRegisteredAppRepository, _roleSettings, _getRoleClaimsQueryHandler);

    // Act
    var vm = await handler.GetItemAsync(query, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(["testapp:user"], vm.User.Roles);
    A.CallTo(() => _getRoleClaimsQueryHandler.GetItemsAsync(A<GetRoleClaimsForRolesQuery>.That.Matches(q => q.Roles.SequenceEqual(new[] { "testapp:user" })), A<CancellationToken>._))
      .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Get_User_For_An_App_Throws_NoRecordException_For_A_User_Of_Another_App()
  {
    // Arrange
    GetUserQuery query = new() { UserId = "test", AppName = "TestApp" };

    A.CallTo(() => _userManager.FindByIdAsync(query.UserId)).Returns(new IdentityUser { Id = "test", UserName = "test" });
    A.CallTo(() => _userRegisteredAppRepository.IsUserRegisteredForAppAsync("test", "TestApp", A<CancellationToken>._)).Returns(false);

    GetUserQueryHandler handler = new(_userManager, _userRepository, _userRegisteredAppRepository, _roleSettings, _getRoleClaimsQueryHandler);

    // Act & Assert
    await Assert.ThrowsAsync<NoRecordException>(() => handler.GetItemAsync(query, TestContext.Current.CancellationToken));
  }
}
