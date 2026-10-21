using System.IO;
using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.AspNetCore.Identity;

using PolyzenKit.Common.Exceptions;

using UserIdentity.Application.Core.Roles.Commands;
using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.Domain.Roles;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Roles.Commands;

public class UpdateRoleCommandHandlerTests
{

  private readonly RoleManager<IdentityRole> _roleManager;
  public UpdateRoleCommandHandlerTests()
  {
    _roleManager = A.Fake<RoleManager<IdentityRole>>();
  }

  [Fact]
  public async Task Update_NonExisting_Role_Throws_NoRecordException()
  {
    // Arrange
    UpdateRoleCommand command = new()
    {
      RoleId = "1",
      RoleName = "Admin"
    };

    A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(default(IdentityRole));

    UpdateRoleCommandHandler handler = new(_roleManager);

    // Act& Assert
    await Assert.ThrowsAsync<NoRecordException>(() => handler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Update_Role_For_An_App_Admin_With_Another_Apps_Role_Throws_NoRecordException()
  {
    // Arrange
    UpdateRoleCommand command = new() { RoleId = "1", RoleName = "testapp:editor", AppName = "TestApp" };

    A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(new IdentityRole { Id = "1", Name = "otherapp:editor" });

    UpdateRoleCommandHandler handler = new(_roleManager);

    // Act & Assert
    await Assert.ThrowsAsync<NoRecordException>(() => handler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));

    A.CallTo(() => _roleManager.UpdateAsync(A<IdentityRole>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Update_Role_For_An_App_Admin_To_A_Name_Outside_The_Apps_Prefix_Throws_InvalidDataException()
  {
    // Arrange
    UpdateRoleCommand command = new() { RoleId = "1", RoleName = "otherapp:admin", AppName = "TestApp" };

    A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(new IdentityRole { Id = "1", Name = "testapp:editor" });

    UpdateRoleCommandHandler handler = new(_roleManager);

    // Act & Assert
    await Assert.ThrowsAsync<InvalidDataException>(() => handler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));

    A.CallTo(() => _roleManager.UpdateAsync(A<IdentityRole>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Update_Existing_Role_Failure_Throws_RecordUpdateException()
  {
    // Arrange
    UpdateRoleCommand command = new()
    {
      RoleId = "1",
      RoleName = "Admin"
    };

    IdentityRole identityRole = new()
    {
      Id = command.RoleId,
      Name = "User"
    };

    A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(identityRole);

    A.CallTo(() => _roleManager.UpdateAsync(identityRole)).Returns(IdentityResult.Failed());

    UpdateRoleCommandHandler handler = new(_roleManager);

    // Act& Assert
    await Assert.ThrowsAsync<RecordUpdateException>(() => handler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Update_Existing_Role_Updates_Successfully()
  {
    // Arrange
    UpdateRoleCommand command = new()
    {
      RoleId = "1",
      RoleName = "Admin"
    };

    IdentityRole identityRole = new()
    {
      Id = command.RoleId,
      Name = "User"
    };

    A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(identityRole);

    A.CallTo(() => _roleManager.UpdateAsync(identityRole)).Returns(IdentityResult.Success);

    UpdateRoleCommandHandler handler = new(_roleManager);

    // Act
    var vm = await handler.UpdateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.IsType<RoleViewModel>(vm);
    Assert.IsType<RoleDTO>(vm.Role);
    Assert.Equal(command.RoleName, vm.Role.Name);
  }
}
