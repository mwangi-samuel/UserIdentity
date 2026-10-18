using System.Collections.Generic;
using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.AspNetCore.Identity;

using PolyzenKit.Common.Exceptions;

using UserIdentity.Application.Core.Roles.Queries;
using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Roles.Queries;

public class GetUserRolesQueryHandlerTests
{
  private readonly RoleManager<IdentityRole> _roleManager;
  private readonly UserManager<IdentityUser> _userManager;


  public GetUserRolesQueryHandlerTests()
  {
    _roleManager = A.Fake<RoleManager<IdentityRole>>();
    _userManager = IdentityFakes.UserManager();
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

    GetUserRolesQueryHandler handler = new(_roleManager, _userManager);

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

    GetUserRolesQueryHandler handler = new(_roleManager, _userManager);

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

    GetUserRolesQueryHandler handler = new(_roleManager, _userManager);

    // Act &  Assert
    await Assert.ThrowsAsync<NoRecordException>(() => handler.GetItemsAsync(query, TestContext.Current.CancellationToken));
  }
}
