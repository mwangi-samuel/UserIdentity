using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using PolyzenKit.Common.Exceptions;
using PolyzenKit.Presentation.Settings;

using UserIdentity.Application.Core.Roles.Queries;
using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Roles.Queries;

public class GetUserRolesQueryHandlerTests
{
  private readonly RoleManager<IdentityRole> _roleManager;
  private readonly UserManager<IdentityUser> _userManager;
  private readonly IUserRegisteredAppRepository _userRegisteredAppRepository;
  private readonly IOptions<RoleSettings> _roleSettings = Options.Create(new RoleSettings { ServiceName = "UserIdentity", AdminRoles = "admin", DefaultRole = "user" });

  public GetUserRolesQueryHandlerTests()
  {
    _roleManager = A.Fake<RoleManager<IdentityRole>>();
    _userManager = IdentityFakes.UserManager();
    _userRegisteredAppRepository = A.Fake<IUserRegisteredAppRepository>();
  }

  [Fact]
  public async Task Get_UserRoles_Returns_UserRoles()
  {
    // Arrange
    GetUserRolesQuery query = new() { UserId = "1" };
    IdentityUser user = new() { Id = "1", UserName = "test" };
    List<string> userRoles = ["Admin", "User"];

    A.CallTo(() => _userManager.FindByIdAsync(query.UserId)).Returns(user);
    A.CallTo(() => _userManager.GetRolesAsync(user)).Returns(userRoles);

    GetUserRolesQueryHandler handler = new(_roleManager, _userManager, _userRegisteredAppRepository, _roleSettings);

    // Act
    var vm = await handler.GetItemsAsync(query, TestContext.Current.CancellationToken);

    // Assert
    Assert.IsType<UserRolesViewModel>(vm);
    Assert.Equal(userRoles.Count, vm.UserRoles.Count);
    Assert.Equal(userRoles, vm.UserRoles);

    foreach (var item in vm.UserRoles)
      Assert.Contains(item, userRoles);

  }

  [Fact]
  public async Task Get_UserRoles_for_NonExisiting_UserRoles_Returns_Zero_UserRoles()
  {
    // Arrange
    GetUserRolesQuery query = new() { UserId = "1" };
    IdentityUser user = new() { Id = "1", UserName = "test" };
    List<string>? userRoles = default;

    A.CallTo(() => _userManager.FindByIdAsync(query.UserId)).Returns(user);
    A.CallTo(() => _userManager.GetRolesAsync(user)).Returns(userRoles!);

    GetUserRolesQueryHandler handler = new(_roleManager, _userManager, _userRegisteredAppRepository, _roleSettings);

    // Act
    var vm = await handler.GetItemsAsync(query, TestContext.Current.CancellationToken);

    // Assert
    Assert.IsType<UserRolesViewModel>(vm);
    Assert.Empty(vm.UserRoles);
  }

  [Fact]
  public async Task Get_UserRoles_for_NonExisiting_User_Throws_NoRecordException()
  {
    // Arrange
    GetUserRolesQuery query = new() { UserId = "1" };
    var user = default(IdentityUser);

    A.CallTo(() => _userManager.FindByIdAsync(query.UserId)).Returns(user);

    GetUserRolesQueryHandler handler = new(_roleManager, _userManager, _userRegisteredAppRepository, _roleSettings);

    // Act &  Assert
    await Assert.ThrowsAsync<NoRecordException>(() => handler.GetItemsAsync(query, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Get_UserRoles_for_An_App_Returns_Only_The_Apps_Roles_And_The_Global_Admin_Role()
  {
    // Arrange
    GetUserRolesQuery query = new() { UserId = "1", AppName = "TestApp" };
    IdentityUser user = new() { Id = "1", UserName = "test" };

    A.CallTo(() => _userManager.FindByIdAsync(query.UserId)).Returns(user);
    A.CallTo(() => _userManager.GetRolesAsync(user)).Returns(["testapp:user", "otherapp:admin", "global:admin", "Administrator"]);
    A.CallTo(() => _userRegisteredAppRepository.IsUserRegisteredForAppAsync("1", "TestApp", A<CancellationToken>._)).Returns(true);

    GetUserRolesQueryHandler handler = new(_roleManager, _userManager, _userRegisteredAppRepository, _roleSettings);

    // Act
    var vm = await handler.GetItemsAsync(query, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(["testapp:user", "global:admin"], vm.UserRoles);
  }

  [Fact]
  public async Task Get_UserRoles_for_An_App_Throws_NoRecordException_For_A_User_Of_Another_App()
  {
    // Arrange
    GetUserRolesQuery query = new() { UserId = "1", AppName = "TestApp" };

    A.CallTo(() => _userManager.FindByIdAsync(query.UserId)).Returns(new IdentityUser { Id = "1", UserName = "test" });
    A.CallTo(() => _userRegisteredAppRepository.IsUserRegisteredForAppAsync("1", "TestApp", A<CancellationToken>._)).Returns(false);

    GetUserRolesQueryHandler handler = new(_roleManager, _userManager, _userRegisteredAppRepository, _roleSettings);

    // Act & Assert
    await Assert.ThrowsAsync<NoRecordException>(() => handler.GetItemsAsync(query, TestContext.Current.CancellationToken));
  }
}
