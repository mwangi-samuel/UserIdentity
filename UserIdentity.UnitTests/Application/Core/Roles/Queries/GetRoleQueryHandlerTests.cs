using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.AspNetCore.Identity;

using PolyzenKit.Common.Exceptions;

using UserIdentity.Application.Core.Roles.Queries;
using UserIdentity.Application.Core.Roles.ViewModels;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Roles.Queries;

public class GetRoleQueryHandlerTests
{
  private readonly RoleManager<IdentityRole> _roleManager;
  public GetRoleQueryHandlerTests()
  {
    _roleManager = A.Fake<RoleManager<IdentityRole>>();

  }

  [Fact]
  public async Task Get_Role_Returns_Role()
  {
    // Arrange
    GetRoleQuery query = new()
    {
      RoleId = "1"
    };

    IdentityRole role = new()
    {
      Id = "1",
      Name = "Admin"
    };

    A.CallTo(() => _roleManager.FindByIdAsync(query.RoleId)).Returns(role);

    GetRoleQueryHandler handler = new(_roleManager);

    // Act
    var vm = await handler.GetItemAsync(query, TestContext.Current.CancellationToken);

    // Assert
    Assert.IsType<RoleViewModel>(vm);
    Assert.Equal(role.Id, vm.Role.Id);
    Assert.Equal(role.Name, vm.Role.Name);
  }

  [Fact]
  public async Task Get_Role_Throws_NoRecordException()
  {
    // Arrange
    GetRoleQuery query = new()
    {
      RoleId = "1"
    };

    A.CallTo(() => _roleManager.FindByIdAsync(query.RoleId)).Returns(default(IdentityRole));

    GetRoleQueryHandler handler = new(_roleManager);

    // Act & Assert
    await Assert.ThrowsAsync<NoRecordException>(() => handler.GetItemAsync(query, TestContext.Current.CancellationToken));

  }

}
