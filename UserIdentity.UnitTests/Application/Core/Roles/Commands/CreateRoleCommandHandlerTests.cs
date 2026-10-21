using System.IO;
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

  [Theory]
  [InlineData("otherapp:editor")]
  [InlineData("global:admin")]
  [InlineData("editor")]
  public async Task Create_Role_For_An_App_Admin_Outside_The_Apps_Prefix_Throws_InvalidDataException(string roleName)
  {
    // Arrange
    CreateRoleCommand command = new() { RoleName = roleName, AppName = "TestApp" };

    CreateRoleCommandHandler handler = new(_roleManager);

    // Act & Assert
    await Assert.ThrowsAsync<InvalidDataException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));

    A.CallTo(() => _roleManager.CreateAsync(A<IdentityRole>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Create_Role_For_An_App_Admin_Creates_A_Role_With_The_Apps_Prefix()
  {
    // Arrange
    CreateRoleCommand command = new() { RoleName = "testapp:editor", AppName = "TestApp" };

    A.CallTo(() => _roleManager.FindByNameAsync(command.RoleName)).Returns(default(IdentityRole));
    A.CallTo(() => _roleManager.CreateAsync(A<IdentityRole>._)).Returns(Task.FromResult(IdentityResult.Success));

    CreateRoleCommandHandler handler = new(_roleManager);

    // Act
    var vm = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal("testapp:editor", vm.Role.Name);
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
