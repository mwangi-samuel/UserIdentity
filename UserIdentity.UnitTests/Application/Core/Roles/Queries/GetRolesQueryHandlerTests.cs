using FakeItEasy;

using UserIdentity.Application.Core.Roles.Queries;
using UserIdentity.Domain.Roles;
using UserIdentity.Persistence.Repositories.Roles;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Roles.Queries;

public class GetRolesQueryHandlerTests
{
  private readonly IRoleRepository _roleRepository = A.Fake<IRoleRepository>();

  [Fact]
  public async Task Get_Roles_For_An_App_Admin_Reads_The_Apps_Roles()
  {
    // Arrange
    A.CallTo(() => _roleRepository.GetRoleDtosAsync("testapp:", A<CancellationToken>._))
      .Returns([new RoleDto { Id = "1", Name = "testapp:admin" }, new RoleDto { Id = "2", Name = "testapp:user" }]);

    var handler = new GetRolesQueryHandler(_roleRepository);

    // Act
    var vm = await handler.GetItemsAsync(
      new GetRolesQuery { AppName = "TestApp" },
      TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal(["testapp:admin", "testapp:user"], vm.Roles.Select(role => role.Name));
  }

  [Fact]
  public async Task Get_Roles_For_A_Global_Admin_Reads_Every_Role()
  {
    // Arrange
    A.CallTo(() => _roleRepository.GetRoleDtosAsync(null, A<CancellationToken>._)).Returns([]);

    var handler = new GetRolesQueryHandler(_roleRepository);

    // Act
    var vm = await handler.GetItemsAsync(new GetRolesQuery(), TestContext.Current.CancellationToken);

    // Assert
    Assert.Empty(vm.Roles);
    A.CallTo(() => _roleRepository.GetRoleDtosAsync(null, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }
}
