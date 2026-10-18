using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.AspNetCore.Identity;

using PolyzenKit.Common.Exceptions;

using UserIdentity.Application.Core.Roles.Commands;
using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Roles.Commands;

public class CreateRoleCommandHandlerTests
{

  private readonly RoleManager<IdentityRole> _roleManager;


  public CreateRoleCommandHandlerTests()
  {
    _roleManager = A.Fake<RoleManager<IdentityRole>>();
  }
  [Fact]
  public async Task Create_Role_When_Role_Exists_Throws_RecordExistsException()
  {
    // Arrange
    CreateRoleCommand command = new() { RoleName = "Admin" };

    A.CallTo(() => _roleManager.FindByNameAsync(command.RoleName)).Returns(new IdentityRole { Id = "1", Name = command.RoleName });

    CreateRoleCommandHandler handler = new(_roleManager);

    // Act & Assert
    await Assert.ThrowsAsync<RecordExistsException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Create_Role_When_Role_Creation_Fails_Throws_RecordCreationException()
  {
    // Arrange
    CreateRoleCommand command = new() { RoleName = "Admin" };


    A.CallTo(() => _roleManager.FindByNameAsync(command.RoleName)).Returns(default(IdentityRole));
    A.CallTo(() => _roleManager.CreateAsync(A<IdentityRole>.That.Matches(r => r.Name == command.RoleName))).Returns(Task.FromResult(IdentityResult.Failed()));

    CreateRoleCommandHandler handler = new(_roleManager);

    // Act & Assert
    await Assert.ThrowsAsync<RecordCreationException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Create_Role_Returns_Role()
  {
    // Arrange
    CreateRoleCommand command = new() { RoleName = "Admin" };

    A.CallTo(() => _roleManager.FindByNameAsync(command.RoleName)).Returns(default(IdentityRole));
    A.CallTo(() => _roleManager.CreateAsync(A<IdentityRole>.That.Matches(r => r.Name == command.RoleName))).Returns(Task.FromResult(IdentityResult.Success));

    CreateRoleCommandHandler handler = new(_roleManager);

    // Act
    var vm = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.IsType<RoleViewModel>(vm);
    Assert.NotNull(vm.Role.Id);
    Assert.Equal(command.RoleName, vm.Role.Name);
  }

}
