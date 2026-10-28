using System.Threading;
using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.AspNetCore.Identity;

using PolyzenKit.Application.Core.Interfaces;
using PolyzenKit.Common.Exceptions;

using UserIdentity.Application.Core.Roles.Commands;
using UserIdentity.Application.Core.Roles.Queries;
using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.Persistence.Repositories.UserRegisteredApps;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Roles.Commands;

public class CreateUserRoleCommandHandlerTests
{
  private readonly RoleManager<IdentityRole> _roleManager;
  private readonly UserManager<IdentityUser> _userManager;
  private readonly IGetItemsQueryHandler<GetUserRolesQuery, UserRolesViewModel> _getUserRolesQueryHandler;
  private readonly IUserRegisteredAppRepository _userRegisteredAppRepository;

  public CreateUserRoleCommandHandlerTests()
  {
    _roleManager = A.Fake<RoleManager<IdentityRole>>();
    _userManager = IdentityFakes.UserManager();
    _getUserRolesQueryHandler = A.Fake<IGetItemsQueryHandler<GetUserRolesQuery, UserRolesViewModel>>();
    _userRegisteredAppRepository = A.Fake<IUserRegisteredAppRepository>();
  }

  [Fact]
  public async Task Create_UserRole_When_No_User_Exists_Throws_NoRecordException()
  {
    // Arrange
    CreateUserRoleCommand command = new() { UserId = "123", RoleId = "Admin123" };
    A.CallTo(() => _userManager.FindByIdAsync(command.UserId)).Returns(default(IdentityUser));

    CreateUserRoleCommandHandler handler = new(_roleManager, _userManager, _userRegisteredAppRepository, _getUserRolesQueryHandler);

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

    CreateUserRoleCommandHandler handler = new(_roleManager, _userManager, _userRegisteredAppRepository, _getUserRolesQueryHandler);

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

    CreateUserRoleCommandHandler handler = new(_roleManager, _userManager, _userRegisteredAppRepository, _getUserRolesQueryHandler);

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

    CreateUserRoleCommandHandler handler = new(_roleManager, _userManager, _userRegisteredAppRepository, _getUserRolesQueryHandler);

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

    CreateUserRoleCommandHandler handler = new(_roleManager, _userManager, _userRegisteredAppRepository, _getUserRolesQueryHandler);

    // Act
    var result = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.NotNull(result);
    Assert.Single(result.UserRoles);
  }

  [Fact]
  public async Task Create_UserRole_For_An_App_Admin_Adds_The_Apps_Role_To_A_User_Of_The_App()
  {
    // Arrange
    CreateUserRoleCommand command = new() { UserId = "123", RoleId = "Editor123", AppName = "TestApp" };
    IdentityRole existingRole = new() { Id = command.RoleId, Name = "testapp:editor" };

    A.CallTo(() => _userManager.FindByIdAsync(command.UserId)).Returns(new IdentityUser { Id = command.UserId });
    A.CallTo(() => _userRegisteredAppRepository.IsUserRegisteredForAppAsync(command.UserId, "TestApp", A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(existingRole);
    A.CallTo(() => _userManager.AddToRoleAsync(A<IdentityUser>._, existingRole.Name)).Returns(Task.FromResult(IdentityResult.Success));

    CreateUserRoleCommandHandler handler = new(_roleManager, _userManager, _userRegisteredAppRepository, _getUserRolesQueryHandler);

    // Act
    await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    A.CallTo(() => _userManager.AddToRoleAsync(A<IdentityUser>._, existingRole.Name)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _getUserRolesQueryHandler.GetItemsAsync(A<GetUserRolesQuery>.That.Matches(q => q.AppName == "TestApp"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Create_UserRole_For_An_App_Admin_Throws_NoRecordException_For_A_User_Of_Another_App()
  {
    // Arrange
    CreateUserRoleCommand command = new() { UserId = "123", RoleId = "Editor123", AppName = "TestApp" };

    A.CallTo(() => _userManager.FindByIdAsync(command.UserId)).Returns(new IdentityUser { Id = command.UserId });
    A.CallTo(() => _userRegisteredAppRepository.IsUserRegisteredForAppAsync(command.UserId, "TestApp", A<CancellationToken>._)).Returns(false);

    CreateUserRoleCommandHandler handler = new(_roleManager, _userManager, _userRegisteredAppRepository, _getUserRolesQueryHandler);

    // Act & Assert
    await Assert.ThrowsAsync<NoRecordException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));

    A.CallTo(() => _userManager.AddToRoleAsync(A<IdentityUser>._, A<string>._)).MustNotHaveHappened();
  }

  [Theory]
  [InlineData("otherapp:admin")]
  [InlineData("global:admin")]
  [InlineData("Administrator")]
  public async Task Create_UserRole_For_An_App_Admin_Throws_NoRecordException_For_A_Role_Outside_The_App(string roleName)
  {
    // Arrange
    CreateUserRoleCommand command = new() { UserId = "123", RoleId = "Role123", AppName = "TestApp" };

    A.CallTo(() => _userManager.FindByIdAsync(command.UserId)).Returns(new IdentityUser { Id = command.UserId });
    A.CallTo(() => _userRegisteredAppRepository.IsUserRegisteredForAppAsync(command.UserId, "TestApp", A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(new IdentityRole { Id = command.RoleId, Name = roleName });

    CreateUserRoleCommandHandler handler = new(_roleManager, _userManager, _userRegisteredAppRepository, _getUserRolesQueryHandler);

    // Act & Assert
    await Assert.ThrowsAsync<NoRecordException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));

    A.CallTo(() => _userManager.AddToRoleAsync(A<IdentityUser>._, A<string>._)).MustNotHaveHappened();
  }
}
