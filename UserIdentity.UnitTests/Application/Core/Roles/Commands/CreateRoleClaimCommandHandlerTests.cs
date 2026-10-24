using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;

using FakeItEasy;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using PolyzenKit.Common.Exceptions;
using PolyzenKit.Infrastructure.Security.Jwt;
using PolyzenKit.Presentation.Settings;

using UserIdentity.Application.Core.Roles.Commands;
using UserIdentity.Application.Core.Roles.ViewModels;
using UserIdentity.UnitTests.TestUtils;

using Xunit;

namespace UserIdentity.UnitTests.Application.Core.Roles.Commands;

public class CreateRoleClaimCommandHandlerTests
{

  private readonly RoleManager<IdentityRole> _roleManager;
  private readonly IJwtTokenHandler _jwtTokenHandler;
  private readonly IOptions<RoleSettings> _roleSettings = Options.Create(new RoleSettings { ServiceName = "UserIdentity", AdminRoles = "admin", DefaultRole = "user" });

  public CreateRoleClaimCommandHandlerTests()
  {
    _roleManager = A.Fake<RoleManager<IdentityRole>>();
    _jwtTokenHandler = A.Fake<IJwtTokenHandler>();
  }

  [Fact]
  public async Task Create_RoleClaim_When_Role_Does_Not_Exist_Throws_NoRecordException()
  {
    // Arrange
    CreateRoleClaimCommand command = new()
    {
      RoleId = "1",
      Resource = "resource",
      Action = "action"
    };

    A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(default(IdentityRole));


    CreateRoleClaimCommandHandler handler = new(_roleManager, _jwtTokenHandler, _roleSettings);

    // Act & Assert
    await Assert.ThrowsAsync<NoRecordException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Create_RoleClaim_For_Another_Apps_Role_Throws_NoRecordException()
  {
    // Arrange
    CreateRoleClaimCommand command = new()
    {
      RoleId = "1",
      Resource = "resource",
      Action = "create",
      AppName = "testapp"
    };

    A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(new IdentityRole { Id = command.RoleId, Name = "otherapp:owner" });

    CreateRoleClaimCommandHandler handler = new(_roleManager, _jwtTokenHandler, _roleSettings);

    // Act & Assert
    await Assert.ThrowsAsync<NoRecordException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));
    A.CallTo(() => _roleManager.AddClaimAsync(A<IdentityRole>._, A<Claim>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Create_RoleClaim_On_The_Global_Admin_Role_Throws_InvalidDataException()
  {
    // Arrange
    CreateRoleClaimCommand command = new()
    {
      RoleId = "1",
      Resource = "resource",
      Action = "create"
    };

    A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(new IdentityRole { Id = command.RoleId, Name = _roleSettings.Value.GlobalAdminRole });

    CreateRoleClaimCommandHandler handler = new(_roleManager, _jwtTokenHandler, _roleSettings);

    // Act & Assert
    await Assert.ThrowsAsync<InvalidDataException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));
    A.CallTo(() => _roleManager.AddClaimAsync(A<IdentityRole>._, A<Claim>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Create_RoleClaim_Stores_The_Scope_In_Lowercase()
  {
    // Arrange
    CreateRoleClaimCommand command = new()
    {
      RoleId = "1",
      Resource = "FileManager-File",
      Action = "Create",
      AppName = "testapp"
    };

    IdentityRole role = new() { Id = command.RoleId, Name = "testapp:owner" };
    A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(role);

    Claim scopeClaim = new("scope", "filemanager-file:create");
    A.CallTo(() => _jwtTokenHandler.GenerateScopeClaim("filemanager-file", "create")).Returns(scopeClaim);
    A.CallTo(() => _roleManager.GetClaimsAsync(role)).Returns(new List<Claim>());
    A.CallTo(() => _roleManager.AddClaimAsync(role, scopeClaim)).Returns(Task.FromResult(IdentityResult.Success));

    CreateRoleClaimCommandHandler handler = new(_roleManager, _jwtTokenHandler, _roleSettings);

    // Act
    var vm = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.Equal("filemanager-file", vm.RoleClaim.Resource);
    Assert.Equal("create", vm.RoleClaim.Action);
    A.CallTo(() => _roleManager.AddClaimAsync(role, scopeClaim)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Create_RoleClaim_When_Role_Claim_Exists_Throws_RecordExistsException()
  {
    // Arrange
    CreateRoleClaimCommand command = new()
    {
      RoleId = "1",
      Resource = "resource",
      Action = "action"
    };

    var scope = command.Resource + ":" + command.Action;


    IdentityRole role = new() { Id = command.RoleId, Name = "Admin" };
    A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(role);

    Claim scopeClaim = new("scope", scope);
    A.CallTo(() => _jwtTokenHandler.GenerateScopeClaim(command.Resource, command.Action)).Returns(scopeClaim);

    List<Claim> roleClaims = [new("scope", scope)];

    A.CallTo(() => _roleManager.GetClaimsAsync(role)).Returns(roleClaims);


    CreateRoleClaimCommandHandler handler = new(_roleManager, _jwtTokenHandler, _roleSettings);

    // Act & Assert
    await Assert.ThrowsAsync<RecordExistsException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Create_RoleClaim_When_Role_Claim_Creation_Fails_Throws_RecordCreationException()
  {
    // Arrange
    CreateRoleClaimCommand command = new()
    {
      RoleId = "1",
      Resource = "resource",
      Action = "action"
    };

    var scope = command.Resource + ":" + command.Action;


    IdentityRole role = new() { Id = command.RoleId, Name = "Admin" };
    A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(role);

    Claim scopeClaim = new("scope", scope);
    A.CallTo(() => _jwtTokenHandler.GenerateScopeClaim(command.Resource, command.Action)).Returns(scopeClaim);

    List<Claim> roleClaims = [];

    A.CallTo(() => _roleManager.GetClaimsAsync(role)).Returns(roleClaims);

    A.CallTo(() => _roleManager.AddClaimAsync(role, scopeClaim)).Returns(Task.FromResult(IdentityResult.Failed()));

    CreateRoleClaimCommandHandler handler = new(_roleManager, _jwtTokenHandler, _roleSettings);

    // Act & Assert
    await Assert.ThrowsAsync<RecordCreationException>(() => handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task Create_RoleClaim_When_Role_Claim_Creation_Succeeds_Returns_RoleClaim()
  {
    // Arrange
    CreateRoleClaimCommand command = new()
    {
      RoleId = "1",
      Resource = "resource",
      Action = "action"
    };

    var scope = command.Resource + ":" + command.Action;


    IdentityRole role = new() { Id = command.RoleId, Name = "Admin" };
    A.CallTo(() => _roleManager.FindByIdAsync(command.RoleId)).Returns(role);

    Claim scopeClaim = new("scope", scope);
    A.CallTo(() => _jwtTokenHandler.GenerateScopeClaim(command.Resource, command.Action)).Returns(scopeClaim);

    List<Claim> roleClaims = [];

    A.CallTo(() => _roleManager.GetClaimsAsync(role)).Returns(roleClaims);

    A.CallTo(() => _roleManager.AddClaimAsync(role, scopeClaim)).Returns(Task.FromResult(IdentityResult.Success));

    CreateRoleClaimCommandHandler handler = new(_roleManager, _jwtTokenHandler, _roleSettings);

    // Act
    var vm = await handler.CreateItemAsync(command, TestStringHelper.UserId, TestContext.Current.CancellationToken);

    // Assert
    Assert.IsType<RoleClaimViewModel>(vm);
    Assert.Equal(command.Resource, vm.RoleClaim.Resource);
    Assert.Equal(command.Action, vm.RoleClaim.Action);
    Assert.Equal(scope, vm.RoleClaim.Scope);
  }
}
