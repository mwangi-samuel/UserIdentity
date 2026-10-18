using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.AspNetCore.Identity;

using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;

using UserIdentity.Application.Core.Roles.Commands;
using UserIdentity.Application.Core.Roles.Queries;
using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Roles.Commands;

public class CreateUserRoleCommandHandlerTests
{
  private readonly RoleManager<IdentityRole> _roleManager;
  private readonly UserManager<IdentityUser> _userManager;
  private readonly IGetItemsQueryHandler<GetUserRolesQuery, UserRolesViewModel> _getUserRolesQueryHandler;


  public CreateUserRoleCommandHandlerTests()
  {
    _roleManager = A.Fake<RoleManager<IdentityRole>>();
    _userManager = IdentityFakes.UserManager();
    _getUserRolesQueryHandler = A.Fake<IGetItemsQueryHandler<GetUserRolesQuery, UserRolesViewModel>>();
  }

  [Fact]
  public async Task Create_UserRole_When_No_User_Exists_Throws_NoRecordException()
  {
    // Arrange
    CreateUserRoleCommand command = new() { UserId = "123", RoleId = "Admin123" };
    A.CallTo(() => _userManager.FindByIdAsync(command.UserId)).Returns(default(IdentityUser));

    CreateUserRoleCommandHandler handler = new(_roleManager, _userManager, _getUserRolesQueryHandler);

    // Act & Assert
    await Assert.ThrowsAsync<NoRecordException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Create_UserRole_When_No_Role_Exists_Throws_NoRecordException()
  {
    // Arrange
    CreateUserRoleCommand command = new() { UserId = "123", RoleId = "Admin123" };
    A.CallTo(() => _userManager.FindByIdAsync(command.UserId)).Returns(new IdentityUser { Id = command.UserId });
    A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(default(IdentityRole));

    CreateUserRoleCommandHandler handler = new(_roleManager, _userManager, _getUserRolesQueryHandler);

    // Act & Assert
    await Assert.ThrowsAsync<NoRecordException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Create_UserRole_When_User_Role_Exists_Throws_RecordExistsException()
  {
    // Arrange
    CreateUserRoleCommand command = new() { UserId = "123", RoleId = "Admin123" };
    IdentityRole existingRole = new() { Id = command.RoleId, Name = "Admin" };

    A.CallTo(() => _userManager.FindByIdAsync(command.UserId)).Returns(new IdentityUser { Id = command.UserId });
    A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(existingRole);
    A.CallTo(() => _userManager.IsInRoleAsync(A<IdentityUser>.That.Matches(u => u.Id == command.UserId), existingRole.Name)).Returns(Task.FromResult(true));

    CreateUserRoleCommandHandler handler = new(_roleManager, _userManager, _getUserRolesQueryHandler);

    // Act & Assert
    await Assert.ThrowsAsync<RecordExistsException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Create_UserRole_When_UserRole_Creation_Fails_Throws_RecordCreationException()
  {
    // Arrange
    CreateUserRoleCommand command = new() { UserId = "123", RoleId = "Admin123" };
    IdentityRole existingRole = new() { Id = command.RoleId, Name = "Admin" };
    A.CallTo(() => _userManager.FindByIdAsync(command.UserId)).Returns(new IdentityUser { Id = command.UserId });
    A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(existingRole);
    A.CallTo(() => _userManager.IsInRoleAsync(A<IdentityUser>.That.Matches(u => u.Id == command.UserId), existingRole.Name)).Returns(Task.FromResult(false));
    A.CallTo(() => _userManager.AddToRoleAsync(A<IdentityUser>.That.Matches(u => u.Id == command.UserId), existingRole.Name)).Returns(Task.FromResult(IdentityResult.Failed()));

    CreateUserRoleCommandHandler handler = new(_roleManager, _userManager, _getUserRolesQueryHandler);

    // Act & Assert
    await Assert.ThrowsAsync<RecordCreationException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Create_UserRole_Returns_UserRoles()
  {
    // Arrange
    CreateUserRoleCommand command = new() { UserId = "123", RoleId = "Admin123" };
    IdentityRole existingRole = new() { Id = command.RoleId, Name = "Admin" };

    A.CallTo(() => _userManager.FindByIdAsync(command.UserId)).Returns(new IdentityUser { Id = command.UserId });
    A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(existingRole);
    A.CallTo(() => _userManager.IsInRoleAsync(A<IdentityUser>.That.Matches(u => u.Id == command.UserId), existingRole.Name)).Returns(Task.FromResult(false));
    A.CallTo(() => _userManager.AddToRoleAsync(A<IdentityUser>.That.Matches(u => u.Id == command.UserId), existingRole.Name)).Returns(Task.FromResult(IdentityResult.Success));
    A.CallTo(() => _getUserRolesQueryHandler.GetItemsAsync(A<GetUserRolesQuery>.That.Matches(q => q.UserId == command.UserId), A<CancellationToken>._)).Returns(new UserRolesViewModel { UserRoles = ["Admin"] });

    CreateUserRoleCommandHandler handler = new(_roleManager, _userManager, _getUserRolesQueryHandler);

    // Act
    var result = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.NotNull(result);
    Assert.Single(result.UserRoles);
  }
}
